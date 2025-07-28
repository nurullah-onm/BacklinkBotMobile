using System.Text.Json;

namespace BacklinkBotMobile
{
    public class ProxyService
    {
        private List<ProxyServer> availableProxies = new List<ProxyServer>();
        private ProxyServer currentProxy = null;
        private Random random = new Random();
        private int currentProxyIndex = 0;

        public List<ProxyServer> AvailableProxies => availableProxies;
        public ProxyServer CurrentProxy => currentProxy;
        public bool IsProxyEnabled { get; set; } = false;

        // PROXY LİSTESİNİ JSON'DAN YÜKLe
        public async Task<bool> LoadProxiesFromJson(string jsonContent)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var proxyResponse = JsonSerializer.Deserialize<ProxyResponse>(jsonContent, options);

                if (proxyResponse?.ServersData?.Data?.Servers != null)
                {
                    availableProxies.Clear();

                    foreach (var rawProxy in proxyResponse.ServersData.Data.Servers)
                    {
                        if (!string.IsNullOrEmpty(rawProxy.Host))
                        {
                            var proxy = rawProxy.ToProxyServer();
                            availableProxies.Add(proxy);
                        }
                    }

                    // LOAD'A GÖRE SIRALA (DÜŞÜKTEN YÜKSEĞE)
                    availableProxies = availableProxies
                        .Where(p => p.Load < 800) // Çok yüklü olanları filtrele
                        .OrderBy(p => p.Load)
                        .ToList();

                    return availableProxies.Count > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Proxy yükleme hatası: {ex.Message}");
            }

            return false;
        }

        // EMBEDDED JSON'DAN YÜKLe
        public async Task<bool> LoadEmbeddedProxies()
        {
            try
            {
                // JSON dosyasını embedded resource olarak yükle
                var jsonContent = GetEmbeddedProxyJson();
                return await LoadProxiesFromJson(jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Embedded proxy yükleme hatası: {ex.Message}");
                return false;
            }
        }

        // EN İYİ PROXY'Yi SEÇ (DÜŞÜK LOAD)
        public ProxyServer GetBestProxy()
        {
            if (availableProxies.Count == 0) return null;

            // En düşük load'a sahip proxy'leri al
            var bestProxies = availableProxies
                .Where(p => p.IsActive && p.Load <= 500)
                .OrderBy(p => p.Load)
                .Take(10)
                .ToList();

            if (bestProxies.Count == 0)
                bestProxies = availableProxies.Take(5).ToList();

            // Rastgele birini seç
            return bestProxies[random.Next(bestProxies.Count)];
        }

        // RASTGELE PROXY SEÇ
        public ProxyServer GetRandomProxy()
        {
            if (availableProxies.Count == 0) return null;

            var activeProxies = availableProxies.Where(p => p.IsActive).ToList();
            if (activeProxies.Count == 0) return null;

            return activeProxies[random.Next(activeProxies.Count)];
        }

        // SIRADAKİ PROXY'Yi AL (ROTATION)
        public ProxyServer GetNextProxy()
        {
            if (availableProxies.Count == 0) return null;

            var activeProxies = availableProxies.Where(p => p.IsActive).ToList();
            if (activeProxies.Count == 0) return null;

            currentProxyIndex = (currentProxyIndex + 1) % activeProxies.Count;
            return activeProxies[currentProxyIndex];
        }

        // ÜLKEYE GÖRE PROXY SEÇ
        public ProxyServer GetProxyByCountry(string countryCode)
        {
            var countryProxies = availableProxies
                .Where(p => p.IsActive && p.Country.Equals(countryCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Load)
                .ToList();

            if (countryProxies.Count == 0) return null;

            return countryProxies[random.Next(countryProxies.Count)];
        }

        // PROXY'Yİ AKTİF ET
        public void SetCurrentProxy(ProxyServer proxy)
        {
            currentProxy = proxy;
            IsProxyEnabled = proxy != null;
        }

        // PROXY'Yİ DEVRE DIŞI BIRAK
        public void DisableProxy()
        {
            currentProxy = null;
            IsProxyEnabled = false;
        }

        // PROXY İSTATİSTİKLERİ
        public ProxyStats GetProxyStats()
        {
            return new ProxyStats
            {
                TotalProxies = availableProxies.Count,
                ActiveProxies = availableProxies.Count(p => p.IsActive),
                Countries = availableProxies.Select(p => p.Country).Distinct().Count(),
                AverageLoad = availableProxies.Count > 0 ? (int)availableProxies.Average(p => p.Load) : 0,
                BestLoad = availableProxies.Count > 0 ? availableProxies.Min(p => p.Load) : 0,
                WorstLoad = availableProxies.Count > 0 ? availableProxies.Max(p => p.Load) : 0
            };
        }

        // PROXY TEST ET
        public async Task<bool> TestProxy(ProxyServer proxy, int timeoutSeconds = 10)
        {
            try
            {
                var handler = new HttpClientHandler()
                {
                    Proxy = new System.Net.WebProxy($"http://{proxy.Host}:{proxy.Port}")
                    {
                        Credentials = string.IsNullOrEmpty(proxy.Password)
                            ? null
                            : new System.Net.NetworkCredential("user", proxy.Password)
                    },
                    UseProxy = true
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                var response = await client.GetAsync("http://httpbin.org/ip");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                proxy.IsActive = false;
                return false;
            }
        }

        // HTTPCLIENT'A PROXY EKLE
        public HttpClientHandler CreateProxyHandler(ProxyServer proxy)
        {
            if (proxy == null) return new HttpClientHandler();

            return new HttpClientHandler()
            {
                Proxy = new System.Net.WebProxy($"http://{proxy.Host}:{proxy.Port}")
                {
                    Credentials = string.IsNullOrEmpty(proxy.Password)
                        ? null
                        : new System.Net.NetworkCredential("user", proxy.Password)
                },
                UseProxy = true
            };
        }

        // EMBEDDED JSON CONTENT
        private string GetEmbeddedProxyJson()
        {
            return @"{
                ""serversData"": {
                    ""data"": {
                        ""servers"": [
                            {""load"": 289, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona11"", ""host"": ""23.158.40.122"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 295, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada42"", ""host"": ""38.96.254.114"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 321, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona6"", ""host"": ""23.158.200.154"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 322, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona8"", ""host"": ""23.158.200.100"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 328, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona9"", ""host"": ""23.158.40.89"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 329, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada43"", ""host"": ""134.195.197.12"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 405, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria11"", ""host"": ""130.195.222.90"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 428, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria5"", ""host"": ""5.253.207.66"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 430, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada28"", ""host"": ""67.220.94.44"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 442, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria4"", ""host"": ""5.253.207.82"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 412, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona5"", ""host"": ""23.158.40.61"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 413, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona3"", ""host"": ""67.220.86.149"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 418, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona2"", ""host"": ""67.220.86.66"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 424, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona4"", ""host"": ""67.220.86.204"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 333, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona13"", ""host"": ""23.158.40.220"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 334, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona12"", ""host"": ""23.158.40.113"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 335, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona7"", ""host"": ""23.158.40.134"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 347, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona10"", ""host"": ""23.158.200.96"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 373, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona16"", ""host"": ""23.158.200.135"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 387, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona15"", ""host"": ""67.220.70.192"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 406, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona14"", ""host"": ""23.158.200.213"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 407, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona18"", ""host"": ""38.114.123.159"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 450, ""country"": ""US"", ""countryName"": ""US-Arizona"", ""aliasName"": ""Arizona17"", ""host"": ""38.114.123.114"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 399, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada36"", ""host"": ""158.51.121.23"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 417, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada35"", ""host"": ""158.51.121.46"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 448, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada27"", ""host"": ""198.57.27.183"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 452, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada29"", ""host"": ""134.195.196.154"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 456, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada25"", ""host"": ""23.162.200.232"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 465, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada21"", ""host"": ""134.195.196.14"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 468, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada18"", ""host"": ""23.162.56.49"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 471, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada26"", ""host"": ""38.111.114.135"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 476, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada15"", ""host"": ""158.51.121.152"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 486, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada24"", ""host"": ""198.57.27.101"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 494, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada23"", ""host"": ""134.195.196.32"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 498, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada16"", ""host"": ""158.51.121.247"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 503, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada22"", ""host"": ""38.111.114.163"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 531, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada7"", ""host"": ""158.51.121.104"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 532, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada19"", ""host"": ""67.220.94.83"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 538, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada8"", ""host"": ""158.51.121.69"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 544, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada20"", ""host"": ""198.57.27.130"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 554, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada17"", ""host"": ""134.195.196.60"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 621, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada4"", ""host"": ""134.195.198.10"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 627, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada3"", ""host"": ""134.195.198.201"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 635, ""country"": ""CA"", ""countryName"": ""Canada"", ""aliasName"": ""Canada2"", ""host"": ""158.51.121.34"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 452, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria12"", ""host"": ""130.195.222.106"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 483, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria3"", ""host"": ""5.253.207.90"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 695, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria41"", ""host"": ""141.227.164.15"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 722, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria33"", ""host"": ""141.227.164.161"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 737, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria40"", ""host"": ""141.227.164.116"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 741, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria39"", ""host"": ""141.227.164.98"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 754, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria38"", ""host"": ""141.227.164.129"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 757, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria44"", ""host"": ""141.227.164.53"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 758, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria43"", ""host"": ""141.227.164.145"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 759, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria26"", ""host"": ""141.227.164.71"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 762, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria42"", ""host"": ""141.227.164.94"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 764, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria21"", ""host"": ""141.227.164.199"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 764, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria36"", ""host"": ""141.227.164.172"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 769, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria37"", ""host"": ""141.227.164.223"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 771, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria20"", ""host"": ""141.227.164.97"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 772, ""country"": ""AT"", ""countryName"": ""Austria"", ""aliasName"": ""Austria31"", ""host"": ""141.227.164.139"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 268, ""country"": ""US"", ""countryName"": ""US-Chicago"", ""aliasName"": ""Chicago12"", ""host"": ""38.128.66.32"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 286, ""country"": ""US"", ""countryName"": ""US-Chicago"", ""aliasName"": ""Chicago9"", ""host"": ""38.64.138.187"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 422, ""country"": ""FR"", ""countryName"": ""France"", ""aliasName"": ""France197"", ""host"": ""141.94.240.100"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 424, ""country"": ""FR"", ""countryName"": ""France"", ""aliasName"": ""France185"", ""host"": ""91.134.35.72"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 447, ""country"": ""FR"", ""countryName"": ""France"", ""aliasName"": ""France25"", ""host"": ""23.134.91.50"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 448, ""country"": ""FR"", ""countryName"": ""France"", ""aliasName"": ""France195"", ""host"": ""57.128.126.34"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 810, ""country"": ""BR"", ""countryName"": ""Brazil"", ""aliasName"": ""Brazil11"", ""host"": ""103.88.232.55"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 929, ""country"": ""BE"", ""countryName"": ""Belgium"", ""aliasName"": ""Belgium7"", ""host"": ""146.70.190.6"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 937, ""country"": ""BE"", ""countryName"": ""Belgium"", ""aliasName"": ""Belgium8"", ""host"": ""146.70.190.2"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 947, ""country"": ""BE"", ""countryName"": ""Belgium"", ""aliasName"": ""Belgium6"", ""host"": ""146.70.190.10"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 1154, ""country"": ""BR"", ""countryName"": ""Brazil"", ""aliasName"": ""Brazil7"", ""host"": ""103.14.27.207"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 350, ""country"": ""DE"", ""countryName"": ""Germany"", ""aliasName"": ""Germany15"", ""host"": ""5.9.10.113"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 380, ""country"": ""DE"", ""countryName"": ""Germany"", ""aliasName"": ""Germany22"", ""host"": ""78.46.244.143"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 420, ""country"": ""DE"", ""countryName"": ""Germany"", ""aliasName"": ""Germany8"", ""host"": ""148.251.197.50"", ""password"": ""treeup123"", ""port"": 80},
                            {""load"": 465, ""country"": ""DE"", ""countryName"": ""Germany"", ""aliasName"": ""Germany31"", ""host"": ""138.201.126.227"", ""password"": ""treeup123"", ""port"": 80}
                        ]
                    }
                }
            }";
        }
    }
}