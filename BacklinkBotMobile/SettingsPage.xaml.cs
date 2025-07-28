using System.Text.Json;

namespace BacklinkBotMobile
{
    public partial class SettingsPage : ContentPage
    {
        // SETTINGS MODEL
        private BotSettings currentSettings;
        private bool isInitializing = true;

        // PROXY SERVICE
        private ProxyService proxyService;
        private Timer autoRotationTimer;
        private bool isAdvancedSettingsVisible = false;

        public SettingsPage()
        {
            InitializeComponent();
            InitializeSettings();
        }

        private async void InitializeSettings()
        {
            // PROXY SERVİS BAŞLAT
            proxyService = new ProxyService();
            await InitializeProxyService();

            // VARSAYILAN AYARLARI YÜKLe
            currentSettings = LoadDefaultSettings();

            // UI'YI AYARLARLA SENKRONIZE ET
            ApplySettingsToUI();

            // EVENT HANDLER'LARI EKLE
            SetupEventHandlers();

            // BAŞLATMA TAMAMLANDI
            isInitializing = false;

            // WELCOME ANİMASYONU
            await PlayWelcomeAnimation();
        }

        private async Task InitializeProxyService()
        {
            try
            {
                // Embedded proxy listesini yükle
                bool proxyLoaded = await proxyService.LoadEmbeddedProxies();

                if (proxyLoaded)
                {
                    var stats = proxyService.GetProxyStats();
                    ProxyCountLabel.Text = stats.TotalProxies.ToString();
                    ProxyLoadLabel.Text = $"{stats.AverageLoad}%";
                    ProxyCountryLabel.Text = stats.Countries.ToString();

                    // Default ayarları yap
                    ProxyModePicker.SelectedIndex = 0; // "Otomatik En İyi"
                    ProxyTimeoutEntry.Text = "10";
                    RotationIntervalEntry.Text = "5";

                    UpdateProxyStatusIndicator(false, "🔴 Proxy Kapalı");
                }
                else
                {
                    await DisplayAlert("⚠️ Uyarı", "Proxy listesi yüklenemedi!", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"Proxy başlatma hatası: {ex.Message}", "Tamam");
            }
        }

        private void SetupEventHandlers()
        {
            // Proxy event handlers
            ProxyEnabledSwitch.Toggled += OnProxyEnabledChanged;
            ProxyModePicker.SelectedIndexChanged += OnProxyModeChanged;
            SpecificProxyPicker.SelectedIndexChanged += OnSpecificProxyChanged;
            AutoRotationSwitch.Toggled += OnAutoRotationToggled;

            // Performance sliders
            SpeedSlider.ValueChanged += OnSpeedChanged;
            TimeoutSlider.ValueChanged += OnTimeoutChanged;
            ParallelSlider.ValueChanged += OnParallelChanged;
            RetrySlider.ValueChanged += OnRetryChanged;
        }

        private async Task PlayWelcomeAnimation()
        {
            this.Opacity = 0;
            this.TranslationY = 50;

            await Task.WhenAll(
                this.FadeTo(1, 600, Easing.CubicOut),
                this.TranslateTo(0, 0, 600, Easing.CubicOut)
            );
        }

        // PROXY YÖNETİMİ METODLARI
        private async void OnProxyEnabledChanged(object sender, ToggledEventArgs e)
        {
            ProxySettingsSection.IsVisible = e.Value;
            ProxyStatusFrame.IsVisible = true;

            if (e.Value)
            {
                // Proxy aktif edildi
                UpdateProxyStatusIndicator(true, "🟢 Proxy Aktif");
                var selectedMode = ProxyModePicker.SelectedItem?.ToString();
                await SetProxyBasedOnMode(selectedMode);
            }
            else
            {
                // Proxy devre dışı bırakıldı
                proxyService.DisableProxy();
                UpdateProxyStatusIndicator(false, "🔴 Proxy Kapalı");
                ActiveProxyFrame.IsVisible = false;

                // Auto rotation'ı durdur
                StopAutoRotation();
            }

            // Settings'e kaydet
            if (!isInitializing)
            {
                currentSettings.ProxyEnabled = e.Value;
            }
        }

        private async void OnProxyModeChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            var selectedMode = picker.SelectedItem?.ToString();

            // Manuel seçim modunda specific picker'ı göster
            bool showManualSelection = selectedMode?.Contains("Manuel") == true;
            ManualProxySection.IsVisible = showManualSelection;

            // Proxy'i seçilen moda göre ayarla
            if (!showManualSelection && ProxyEnabledSwitch.IsToggled)
            {
                await SetProxyBasedOnMode(selectedMode);
            }
        }

        private async void OnSpecificProxyChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            var selectedProxy = picker.SelectedItem?.ToString();

            if (!string.IsNullOrEmpty(selectedProxy))
            {
                // Seçilen proxy string'inden proxy objesi oluştur
                var proxy = ParseProxyFromString(selectedProxy);
                if (proxy != null)
                {
                    proxyService.SetCurrentProxy(proxy);
                    UpdateActiveProxyDisplay(proxy);
                }
            }
        }

        private ProxyServer ParseProxyFromString(string proxyString)
        {
            try
            {
                // Örnek: "🇺🇸 Arizona11 (23.158.40.122) - Load: 289"
                var parts = proxyString.Split('(', ')');
                if (parts.Length >= 2)
                {
                    var hostPort = parts[1].Split(':');
                    var host = hostPort[0];
                    var port = hostPort.Length > 1 ? int.Parse(hostPort[1]) : 80;

                    // Alias name'i çıkar
                    var aliasStart = proxyString.IndexOf(' ') + 1;
                    var aliasEnd = proxyString.IndexOf(' ', aliasStart);
                    var alias = proxyString.Substring(aliasStart, aliasEnd - aliasStart);

                    // Load'u çıkar
                    var loadMatch = System.Text.RegularExpressions.Regex.Match(proxyString, @"Load: (\d+)");
                    var load = loadMatch.Success ? int.Parse(loadMatch.Groups[1].Value) : 0;

                    // Country'yi çıkar
                    var country = ExtractCountryFromEmoji(proxyString);

                    return new ProxyServer
                    {
                        Host = host,
                        Port = port,
                        AliasName = alias,
                        Load = load,
                        Country = country,
                        Password = "treeup123",
                        IsActive = true
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Proxy parse hatası: {ex.Message}");
            }

            return null;
        }

        private string ExtractCountryFromEmoji(string text)
        {
            if (text.Contains("🇺🇸")) return "US";
            if (text.Contains("🇨🇦")) return "CA";
            if (text.Contains("🇦🇹")) return "AT";
            if (text.Contains("🇧🇪")) return "BE";
            if (text.Contains("🇧🇷")) return "BR";
            if (text.Contains("🇩🇪")) return "DE";
            if (text.Contains("🇫🇷")) return "FR";
            return "XX";
        }

        private async Task SetProxyBasedOnMode(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return;

            ProxyServer selectedProxy = null;

            try
            {
                if (mode.Contains("Otomatik En İyi"))
                {
                    selectedProxy = proxyService.GetBestProxy();
                }
                else if (mode.Contains("Rastgele"))
                {
                    selectedProxy = proxyService.GetRandomProxy();
                }
                else if (mode.Contains("Sıralı"))
                {
                    selectedProxy = proxyService.GetNextProxy();
                }
                else if (mode.Contains("🇺🇸") || mode.Contains("ABD"))
                {
                    selectedProxy = proxyService.GetProxyByCountry("US");
                }
                else if (mode.Contains("🇨🇦") || mode.Contains("Kanada"))
                {
                    selectedProxy = proxyService.GetProxyByCountry("CA");
                }
                else if (mode.Contains("🇦🇹") || mode.Contains("Avusturya"))
                {
                    selectedProxy = proxyService.GetProxyByCountry("AT");
                }
                else if (mode.Contains("🇧🇪") || mode.Contains("Belçika"))
                {
                    selectedProxy = proxyService.GetProxyByCountry("BE");
                }
                else if (mode.Contains("🇧🇷") || mode.Contains("Brezilya"))
                {
                    selectedProxy = proxyService.GetProxyByCountry("BR");
                }
                else if (mode.Contains("🇩🇪") || mode.Contains("Almanya"))
                {
                    selectedProxy = proxyService.GetProxyByCountry("DE");
                }
                else if (mode.Contains("🇫🇷") || mode.Contains("Fransa"))
                {
                    selectedProxy = proxyService.GetProxyByCountry("FR");
                }

                if (selectedProxy != null)
                {
                    proxyService.SetCurrentProxy(selectedProxy);
                    UpdateActiveProxyDisplay(selectedProxy);
                }
                else
                {
                    await DisplayAlert("⚠️ Uyarı", "Seçilen kriterlere uygun proxy bulunamadı!", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"Proxy seçim hatası: {ex.Message}", "Tamam");
            }
        }

        private void UpdateActiveProxyDisplay(ProxyServer proxy)
        {
            if (proxy != null)
            {
                ActiveProxyFrame.IsVisible = true;
                ActiveProxyNameLabel.Text = $"{proxy.Country.GetCountryEmoji()} {proxy.AliasName}";
                ActiveProxyDetailsLabel.Text = $"{proxy.Host}:{proxy.Port} • Load: {proxy.Load.GetLoadDescription()}";

                // Load'a göre renk ayarla
                var loadColor = proxy.Load.GetLoadColor();
                ActiveProxyDetailsLabel.TextColor = loadColor;
            }
            else
            {
                ActiveProxyFrame.IsVisible = false;
            }
        }

        private void UpdateProxyStatusIndicator(bool isConnected, string statusText)
        {
            ProxyStatusIndicator.Fill = new SolidColorBrush(isConnected ? Colors.Green : Colors.Red);
            ProxyStatusLabel.Text = statusText;
        }

        private void UpdateProxyStats()
        {
            var stats = proxyService.GetProxyStats();
            ProxyCountLabel.Text = stats.TotalProxies.ToString();
            ProxyLoadLabel.Text = $"{stats.AverageLoad}%";
            ProxyCountryLabel.Text = stats.Countries.ToString();
        }

        // PROXY TEST VE YÖNETİM METODLARI
        private async void OnTestProxyClicked(object sender, EventArgs e)
        {
            await AnimateButton(TestProxyButton);

            var currentProxy = proxyService.CurrentProxy;

            if (currentProxy == null)
            {
                await DisplayAlert("⚠️ Uyarı", "Önce bir proxy seçin!", "Tamam");
                return;
            }

            TestProxyButton.Text = "🧪 Test Ediliyor...";
            TestProxyButton.IsEnabled = false;

            try
            {
                var timeout = int.TryParse(ProxyTimeoutEntry.Text, out int t) ? t : 10;
                bool isWorking = await proxyService.TestProxy(currentProxy, timeout);

                string title = isWorking ? "✅ Başarılı!" : "❌ Başarısız!";
                string message = $"{currentProxy.Country.GetCountryEmoji()} {currentProxy.AliasName}\n" +
                               $"🌐 {currentProxy.Host}:{currentProxy.Port}\n" +
                               $"⚡ Load: {currentProxy.Load.GetLoadDescription()}\n" +
                               $"⏱️ Timeout: {timeout}s";

                await DisplayAlert(title, message, "Tamam");

                // Başarısız proxy'yi otomatik atla
                if (!isWorking && SkipFailedProxySwitch.IsToggled)
                {
                    await SwitchToNextWorkingProxy();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"Test sırasında hata:\n{ex.Message}", "Tamam");
            }
            finally
            {
                TestProxyButton.Text = "🧪 Test Et";
                TestProxyButton.IsEnabled = true;
            }
        }

        private async Task SwitchToNextWorkingProxy()
        {
            var nextProxy = proxyService.GetNextProxy();
            if (nextProxy != null)
            {
                proxyService.SetCurrentProxy(nextProxy);
                UpdateActiveProxyDisplay(nextProxy);

                await DisplayAlert("🔄 Proxy Değiştirildi",
                                 $"Yeni proxy seçildi:\n{nextProxy.Country.GetCountryEmoji()} {nextProxy.AliasName}",
                                 "Tamam");
            }
        }

        private async void OnChangeProxyClicked(object sender, EventArgs e)
        {
            await AnimateButton(ChangeProxyButton);

            // Mevcut moda göre yeni proxy seç
            var selectedMode = ProxyModePicker.SelectedItem?.ToString();
            await SetProxyBasedOnMode(selectedMode);
        }

        private void OnProxyAdvancedSettingsClicked(object sender, EventArgs e)
        {
            isAdvancedSettingsVisible = !isAdvancedSettingsVisible;
            AdvancedProxySettings.IsVisible = isAdvancedSettingsVisible;

            ProxySettingsButton.Text = isAdvancedSettingsVisible ? "⬆️ Gizle" : "⚙️ Ayarlar";
        }

        // AUTO ROTATION YÖNETİMİ
        private void OnAutoRotationToggled(object sender, ToggledEventArgs e)
        {
            RotationIntervalGrid.IsVisible = e.Value;

            if (e.Value)
            {
                StartAutoRotation();
            }
            else
            {
                StopAutoRotation();
            }
        }

        private void StartAutoRotation()
        {
            StopAutoRotation(); // Mevcut timer'ı durdur

            var intervalMinutes = int.TryParse(RotationIntervalEntry.Text, out int interval) ? interval : 5;
            var intervalMs = intervalMinutes * 60 * 1000; // Dakikayı milisaniyeye çevir

            autoRotationTimer = new Timer(async _ =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var nextProxy = proxyService.GetNextProxy();
                    if (nextProxy != null)
                    {
                        proxyService.SetCurrentProxy(nextProxy);
                        UpdateActiveProxyDisplay(nextProxy);
                    }
                });
            }, null, intervalMs, intervalMs);
        }

        private void StopAutoRotation()
        {
            autoRotationTimer?.Dispose();
            autoRotationTimer = null;
        }

        private async void OnRefreshProxyPoolClicked(object sender, EventArgs e)
        {
            await AnimateButton(RefreshProxyPoolButton);

            RefreshProxyPoolButton.Text = "🔄 Yenileniyor...";
            RefreshProxyPoolButton.IsEnabled = false;

            try
            {
                // Proxy listesini yeniden yükle
                bool success = await proxyService.LoadEmbeddedProxies();

                if (success)
                {
                    UpdateProxyStats();
                    await DisplayAlert("✅ Başarılı", "Proxy listesi yenilendi!", "Tamam");
                }
                else
                {
                    await DisplayAlert("❌ Hata", "Proxy listesi yenilenemedi!", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"Yenileme hatası:\n{ex.Message}", "Tamam");
            }
            finally
            {
                RefreshProxyPoolButton.Text = "🔄 Proxy Listesini Yenile";
                RefreshProxyPoolButton.IsEnabled = true;
            }
        }

        // VARSAYILAN AYARLARI YÜKLe
        private BotSettings LoadDefaultSettings()
        {
            try
            {
                // PREFERENCES'DAN YÜKLe (MAUI için)
                var settingsJson = Preferences.Get("BotSettings", "");

                if (!string.IsNullOrEmpty(settingsJson))
                {
                    return JsonSerializer.Deserialize<BotSettings>(settingsJson);
                }
            }
            catch (Exception ex)
            {
                DisplayAlert("⚠️ Uyarı", $"Ayarlar yüklenirken hata: {ex.Message}", "Tamam");
            }

            // VARSAYILAN AYARLAR
            return new BotSettings
            {
                ProcessingSpeed = 5,
                TimeoutSeconds = 30,
                ParallelThreads = 3,
                RetryCount = 2,
                AutoCaptcha = true,
                FormValidation = true,
                RandomDelay = true,
                SmartDetection = true,
                UserAgentRotation = true,
                ProxyEnabled = false,
                SslVerification = true,
                StealthMode = false,
                DetailedLogging = true,
                AutoReport = true,
                StatisticsEnabled = true,
                ErrorLogging = true
            };
        }

        // UI'YI AYARLARLA SENKRONIZE ET
        private void ApplySettingsToUI()
        {
            SpeedSlider.Value = currentSettings.ProcessingSpeed;
            TimeoutSlider.Value = currentSettings.TimeoutSeconds;
            ParallelSlider.Value = currentSettings.ParallelThreads;
            RetrySlider.Value = currentSettings.RetryCount;

            CaptchaSwitch.IsToggled = currentSettings.AutoCaptcha;
            ValidationSwitch.IsToggled = currentSettings.FormValidation;
            RandomDelaySwitch.IsToggled = currentSettings.RandomDelay;
            SmartDetectionSwitch.IsToggled = currentSettings.SmartDetection;

            UserAgentSwitch.IsToggled = currentSettings.UserAgentRotation;
            ProxySwitch.IsToggled = currentSettings.ProxyEnabled;
            ProxyEnabledSwitch.IsToggled = currentSettings.ProxyEnabled;
            SslSwitch.IsToggled = currentSettings.SslVerification;
            StealthSwitch.IsToggled = currentSettings.StealthMode;

            DetailedLogSwitch.IsToggled = currentSettings.DetailedLogging;
            AutoReportSwitch.IsToggled = currentSettings.AutoReport;
            StatsSwitch.IsToggled = currentSettings.StatisticsEnabled;
            ErrorLogSwitch.IsToggled = currentSettings.ErrorLogging;

            UpdateLabels();
        }

        // LABEL'LARI GÜNCELLE
        private void UpdateLabels()
        {
            SpeedValueLabel.Text = $"{GetSpeedText(currentSettings.ProcessingSpeed)} ({currentSettings.ProcessingSpeed})";
            TimeoutValueLabel.Text = $"{currentSettings.TimeoutSeconds} saniye";
            ParallelValueLabel.Text = $"{currentSettings.ParallelThreads} thread";
            RetryValueLabel.Text = $"{currentSettings.RetryCount} kez";
        }

        // SLIDER EVENT HANDLERS
        private void OnSpeedChanged(object sender, ValueChangedEventArgs e)
        {
            if (isInitializing) return;

            currentSettings.ProcessingSpeed = (int)e.NewValue;
            SpeedValueLabel.Text = $"{GetSpeedText(currentSettings.ProcessingSpeed)} ({currentSettings.ProcessingSpeed})";
        }

        private void OnTimeoutChanged(object sender, ValueChangedEventArgs e)
        {
            if (isInitializing) return;

            currentSettings.TimeoutSeconds = (int)e.NewValue;
            TimeoutValueLabel.Text = $"{currentSettings.TimeoutSeconds} saniye";
        }

        private void OnParallelChanged(object sender, ValueChangedEventArgs e)
        {
            if (isInitializing) return;

            currentSettings.ParallelThreads = (int)e.NewValue;
            ParallelValueLabel.Text = $"{currentSettings.ParallelThreads} thread";
        }

        private void OnRetryChanged(object sender, ValueChangedEventArgs e)
        {
            if (isInitializing) return;

            currentSettings.RetryCount = (int)e.NewValue;
            RetryValueLabel.Text = $"{currentSettings.RetryCount} kez";
        }

        // NAVIGATION METHODS
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await AnimateButton(BackButton);

            // AYARLARI OTOMATİK KAYDET
            await SaveSettings();

            await Shell.Current.GoToAsync("..");
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            await AnimateButton(SaveButton);
            await SaveSettings();

            await DisplayAlert("💾 Kaydedildi",
                             "Ayarlar başarıyla kaydedildi!\n\n" +
                             "🚀 Yeni ayarlar bir sonraki bot çalıştırmasında aktif olacak.",
                             "Tamam");
        }

        // SETTINGS MANAGEMENT
        private async Task SaveSettings()
        {
            try
            {
                // SWITCH'LERDEN DEĞERLERİ AL
                currentSettings.AutoCaptcha = CaptchaSwitch.IsToggled;
                currentSettings.FormValidation = ValidationSwitch.IsToggled;
                currentSettings.RandomDelay = RandomDelaySwitch.IsToggled;
                currentSettings.SmartDetection = SmartDetectionSwitch.IsToggled;

                currentSettings.UserAgentRotation = UserAgentSwitch.IsToggled;
                currentSettings.ProxyEnabled = ProxyEnabledSwitch.IsToggled;
                currentSettings.SslVerification = SslSwitch.IsToggled;
                currentSettings.StealthMode = StealthSwitch.IsToggled;

                currentSettings.DetailedLogging = DetailedLogSwitch.IsToggled;
                currentSettings.AutoReport = AutoReportSwitch.IsToggled;
                currentSettings.StatisticsEnabled = StatsSwitch.IsToggled;
                currentSettings.ErrorLogging = ErrorLogSwitch.IsToggled;

                // JSON'A ÇEVİR VE KAYDET
                var settingsJson = JsonSerializer.Serialize(currentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                Preferences.Set("BotSettings", settingsJson);

                // PROXY AYARLARINI KAYDET
                if (currentSettings.ProxyEnabled && proxyService.CurrentProxy != null)
                {
                    var proxyJson = JsonSerializer.Serialize(proxyService.CurrentProxy);
                    Preferences.Set("CurrentProxy", proxyJson);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"Ayarlar kaydedilirken hata oluştu:\n{ex.Message}", "Tamam");
            }
        }

        // SYSTEM ACTIONS
        private async void OnResetClicked(object sender, EventArgs e)
        {
            await AnimateButton(ResetButton);

            bool shouldReset = await DisplayAlert("🔄 Sıfırla",
                "Tüm ayarları varsayılan değerlere sıfırlamak istediğinizden emin misiniz?",
                "Evet", "Hayır");

            if (shouldReset)
            {
                // Proxy'yi deaktif et
                proxyService.DisableProxy();
                StopAutoRotation();

                currentSettings = LoadDefaultSettings();
                ApplySettingsToUI();
                await SaveSettings();

                // UI'yi sıfırla
                ProxyEnabledSwitch.IsToggled = false;
                UpdateProxyStatusIndicator(false, "🔴 Proxy Kapalı");
                ActiveProxyFrame.IsVisible = false;

                await DisplayAlert("✅ Sıfırlandı",
                    "Tüm ayarlar varsayılan değerlere sıfırlandı!", "Tamam");
            }
        }

        private async void OnExportClicked(object sender, EventArgs e)
        {
            await AnimateButton(ExportButton);

            try
            {
                // Proxy ayarlarını da dahil et
                var exportData = new
                {
                    Settings = currentSettings,
                    ProxyEnabled = proxyService.IsProxyEnabled,
                    CurrentProxy = proxyService.CurrentProxy,
                    ProxyStats = proxyService.GetProxyStats(),
                    ExportDate = DateTime.Now
                };

                var settingsJson = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var fileName = $"BacklinkBot_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                await File.WriteAllTextAsync(filePath, settingsJson);

                // DOSYAYI PAYLAŞ
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "BacklinkBot Ayarları",
                    File = new ShareFile(filePath)
                });

                await DisplayAlert("📤 Dışa Aktarıldı",
                    $"Ayarlar başarıyla dışa aktarıldı!\n\nDosya: {fileName}", "Tamam");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"Dışa aktarma hatası:\n{ex.Message}", "Tamam");
            }
        }

        private async void OnImportClicked(object sender, EventArgs e)
        {
            await AnimateButton(ImportButton);

            try
            {
                var result = await FilePicker.Default.PickAsync();

                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    using var reader = new StreamReader(stream);

                    var settingsJson = await reader.ReadToEndAsync();

                    // Eski format için fallback
                    try
                    {
                        var importData = JsonSerializer.Deserialize<JsonElement>(settingsJson);

                        BotSettings importedSettings;
                        if (importData.TryGetProperty("Settings", out var settingsElement))
                        {
                            importedSettings = JsonSerializer.Deserialize<BotSettings>(settingsElement.GetRawText());
                        }
                        else
                        {
                            importedSettings = JsonSerializer.Deserialize<BotSettings>(settingsJson);
                        }

                        if (importedSettings != null)
                        {
                            currentSettings = importedSettings;
                            ApplySettingsToUI();
                            await SaveSettings();

                            await DisplayAlert("📥 İçe Aktarıldı",
                                "Ayarlar başarıyla içe aktarıldı ve uygulandı!", "Tamam");
                        }
                    }
                    catch
                    {
                        // Eski format deneme
                        var importedSettings = JsonSerializer.Deserialize<BotSettings>(settingsJson);
                        if (importedSettings != null)
                        {
                            currentSettings = importedSettings;
                            ApplySettingsToUI();
                            await SaveSettings();

                            await DisplayAlert("📥 İçe Aktarıldı",
                                "Ayarlar başarıyla içe aktarıldı!", "Tamam");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"İçe aktarma hatası:\n{ex.Message}", "Tamam");
            }
        }

        private async void OnTestClicked(object sender, EventArgs e)
        {
            await AnimateButton(TestButton);

            // TEST URL'Sİ
            string testUrl = "https://httpbin.org/delay/1";

            await DisplayAlert("🧪 Test Başlıyor",
                $"Mevcut ayarlarla test URL'si test ediliyor...\n\n" +
                $"⚡ Hız: {GetSpeedText(currentSettings.ProcessingSpeed)}\n" +
                $"⏱️ Timeout: {currentSettings.TimeoutSeconds}s\n" +
                $"🔄 Paralel: {currentSettings.ParallelThreads}x\n" +
                $"🔁 Retry: {currentSettings.RetryCount}x\n" +
                $"🌐 Proxy: {(currentSettings.ProxyEnabled ? "Aktif" : "Kapalı")}", "Başlat");

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // HttpClient oluştur (proxy dahil)
                HttpClientHandler handler = currentSettings.ProxyEnabled && proxyService.CurrentProxy != null
                    ? proxyService.CreateProxyHandler(proxyService.CurrentProxy)
                    : new HttpClientHandler();

                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(currentSettings.TimeoutSeconds);

                    if (currentSettings.UserAgentRotation)
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
                    }

                    var response = await client.GetAsync(testUrl);
                    stopwatch.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        var proxyInfo = currentSettings.ProxyEnabled && proxyService.CurrentProxy != null
                            ? $"\n🌐 Proxy: {proxyService.CurrentProxy.Country.GetCountryEmoji()} {proxyService.CurrentProxy.AliasName}"
                            : "\n🌐 Proxy: Kullanılmadı";

                        await DisplayAlert("✅ Test Başarılı",
                            $"Ayarlar düzgün çalışıyor!\n\n" +
                            $"📊 Durum: {response.StatusCode}\n" +
                            $"⏱️ Süre: {stopwatch.ElapsedMilliseconds}ms\n" +
                            $"🌐 URL: {testUrl}" +
                            proxyInfo, "Tamam");
                    }
                    else
                    {
                        await DisplayAlert("⚠️ Test Uyarısı",
                            $"HTTP yanıtı: {response.StatusCode}\n" +
                            $"Süre: {stopwatch.ElapsedMilliseconds}ms", "Tamam");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Test Hatası",
                    $"Test sırasında hata oluştu:\n{ex.Message}\n\n" +
                    $"💡 Timeout, proxy veya internet bağlantısını kontrol edin.", "Tamam");
            }
        }

        // HELPER METODLAR
        private string GetSpeedText(int speed)
        {
            return speed switch
            {
                1 => "Çok Yavaş",
                2 => "Yavaş",
                3 => "Düşük",
                4 => "Düşük-Orta",
                5 => "Orta",
                6 => "Orta-Hızlı",
                7 => "Hızlı",
                8 => "Çok Hızlı",
                9 => "Turbo",
                10 => "Maximum",
                _ when speed < 1 => "Çok Yavaş",
                _ when speed > 10 => "Maximum",
                _ => "Orta"
            };
        }

        private string GetRandomUserAgent()
        {
            var userAgents = new[]
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/121.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:109.0) Gecko/20100101 Firefox/121.0",
                "Mozilla/5.0 (Android 12; Mobile; rv:109.0) Gecko/109.0 Firefox/109.0",
                "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1"
            };

            var random = new Random();
            return userAgents[random.Next(userAgents.Length)];
        }

        private async Task AnimateButton(Button button)
        {
            try
            {
                await Task.WhenAll(
                    button.ScaleTo(0.95, 100, Easing.CubicOut),
                    button.FadeTo(0.8, 100, Easing.CubicOut)
                );

                await Task.WhenAll(
                    button.ScaleTo(1, 100, Easing.CubicOut),
                    button.FadeTo(1, 100, Easing.CubicOut)
                );
            }
            catch { }
        }

        // ÖZEL PROXY ACCESS METODLARI (BACKLINK PAGE İÇİN)
        public BotSettings GetCurrentSettings()
        {
            return currentSettings;
        }

        public ProxyService GetProxyService()
        {
            return proxyService;
        }

        public bool IsProxyEnabled()
        {
            return currentSettings.ProxyEnabled && proxyService.IsProxyEnabled;
        }

        // LIFECYCLE MANAGEMENT
        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Timer'ı temizle
            StopAutoRotation();

            // Ayarları kaydet
            if (!isInitializing)
            {
                _ = SaveSettings();
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Proxy stats'ı güncelle
            if (proxyService != null)
            {
                UpdateProxyStats();
            }
        }

        // CLEANUP
        ~SettingsPage()
        {
            StopAutoRotation();
        }
    }

    // BOT SETTINGS MODEL (GENIŞLETILMIŞ)
    public class BotSettings
    {
        // PERFORMANS AYARLARI
        public int ProcessingSpeed { get; set; } = 5;
        public int TimeoutSeconds { get; set; } = 30;
        public int ParallelThreads { get; set; } = 3;
        public int RetryCount { get; set; } = 2;

        // FORM AYARLARI
        public bool AutoCaptcha { get; set; } = true;
        public bool FormValidation { get; set; } = true;
        public bool RandomDelay { get; set; } = true;
        public bool SmartDetection { get; set; } = true;

        // GÜVENLİK AYARLARI
        public bool UserAgentRotation { get; set; } = true;
        public bool ProxyEnabled { get; set; } = false;
        public bool SslVerification { get; set; } = true;
        public bool StealthMode { get; set; } = false;

        // RAPOR AYARLARI
        public bool DetailedLogging { get; set; } = true;
        public bool AutoReport { get; set; } = true;
        public bool StatisticsEnabled { get; set; } = true;
        public bool ErrorLogging { get; set; } = true;

        // YENİ PROXY AYARLARI
        public string ProxyMode { get; set; } = "🚀 Otomatik En İyi";
        public bool AutoProxyRotation { get; set; } = true;
        public int ProxyTimeoutSeconds { get; set; } = 10;
        public int RotationIntervalMinutes { get; set; } = 5;
        public bool SkipFailedProxy { get; set; } = true;

        // GELIŞMIŞ AYARLAR
        public int MaxRequestsPerProxy { get; set; } = 10;
        public bool EnableProxyHealthCheck { get; set; } = true;
        public string PreferredCountries { get; set; } = "US,CA,AT"; // Virgülle ayrılmış ülke kodları
        public int MinProxyLoad { get; set; } = 0;
        public int MaxProxyLoad { get; set; } = 800;
    }

    // SETTINGS EXTENSIONS
    public static class SettingsExtensions
    {
        public static int GetDelayFromSpeed(this BotSettings settings)
        {
            return settings.ProcessingSpeed switch
            {
                1 => 5000,   // 5 saniye
                2 => 4000,   // 4 saniye
                3 => 3000,   // 3 saniye
                4 => 2500,   // 2.5 saniye
                5 => 2000,   // 2 saniye
                6 => 1500,   // 1.5 saniye
                7 => 1000,   // 1 saniye
                8 => 800,    // 0.8 saniye
                9 => 500,    // 0.5 saniye
                10 => 300,   // 0.3 saniye
                _ => 2000    // Default 2 saniye
            };
        }

        public static int GetRandomDelay(this BotSettings settings)
        {
            if (!settings.RandomDelay) return settings.GetDelayFromSpeed();

            var baseDelay = settings.GetDelayFromSpeed();
            var random = new Random();
            var variation = (int)(baseDelay * 0.3); // %30 varyasyon

            return baseDelay + random.Next(-variation, variation + 1);
        }

        public static bool ShouldUseProxy(this BotSettings settings)
        {
            return settings.ProxyEnabled && !string.IsNullOrEmpty(settings.ProxyMode);
        }

        public static TimeSpan GetHttpTimeout(this BotSettings settings)
        {
            return TimeSpan.FromSeconds(settings.TimeoutSeconds);
        }

        public static string[] GetPreferredCountries(this BotSettings settings)
        {
            if (string.IsNullOrEmpty(settings.PreferredCountries))
                return new[] { "US", "CA", "AT" };

            return settings.PreferredCountries.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
    }
}