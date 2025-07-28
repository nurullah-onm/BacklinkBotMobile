using System.Collections.ObjectModel;
using AngleSharp.Html.Parser;
using HtmlAgilityPack;
using ScrapySharp.Network;
using System.Text.RegularExpressions;

namespace BacklinkBotMobile
{
    public partial class BacklinkPage : ContentPage
    {
        // COLLECTIONS VE VERİ YÖNETİMİ
        private ObservableCollection<string> urlList;
        private CancellationTokenSource cancellationTokenSource;
        private bool isRunning = false;

        // İSTATİSTİK VERİLERİ
        private int totalProcessed = 0;
        private int successCount = 0;
        private int failedCount = 0;

        // AYARLAR
        private int currentSpeed = 5;
        private int currentTimeout = 30;

        // PROXY SERVİS
        private ProxyService proxyService;
        private bool useProxy = false;
        private string proxyMode = "🚀 Otomatik En İyi";
        private bool autoProxyRotation = true;
        private int requestsSinceProxyChange = 0;
        private const int MAX_REQUESTS_PER_PROXY = 10;

        // HTTP CLIENT - SÜPER HIZLI
        private HttpClient httpClient;

        // CAPTCHA PATTERN'LERİ
        private static readonly Regex[] CaptchaPatterns = {
            new Regex(@"<span[^>]*style[^>]*background-color[^>]*>[^<]*</span>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"<span[^>]*id=[""']captcha[""'][^>]*>[^<]*</span>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"<span[^>]*class=[""']captcha[""'][^>]*>[^<]*</span>", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        public BacklinkPage()
        {
            InitializeComponent();
            InitializePage();
        }

        private async void InitializePage()
        {
            // URL LİSTESİ BAŞLAT
            urlList = new ObservableCollection<string>();
            UrlCollectionView.ItemsSource = urlList;

            // PROXY SERVİS BAŞLAT
            proxyService = new ProxyService();
            await InitializeProxyService();

            // HTTP CLIENT BAŞLAT
            InitializeHttpClient();

            // WELCOME ANİMASYONU
            await PlayWelcomeAnimation();

            // UI GÜNCELLE
            UpdateSpeedLabel();
            UpdateTimeoutLabel();
            UpdateProxyUI();

            // PROXY PICKER AYARLA
            ProxyModePicker.SelectedIndex = 0; // Otomatik En İyi
        }

        private async Task InitializeProxyService()
        {
            try
            {
                bool proxyLoaded = await proxyService.LoadEmbeddedProxies();

                if (proxyLoaded)
                {
                    var stats = proxyService.GetProxyStats();
                    ProxyCountLabel.Text = stats.TotalProxies.ToString();
                    ProxyLoadLabel.Text = $"{stats.AverageLoad}%";
                    ProxyCountryLabel.Text = stats.Countries.ToString();

                    ProxyStatsGrid.IsVisible = true;
                    LogProxyMessage($"✅ {stats.TotalProxies} proxy yüklendi, {stats.Countries} ülke mevcut");

                    // İlk proxy'yi ayarla
                    if (useProxy)
                    {
                        await SetBestProxy();
                    }
                }
                else
                {
                    LogProxyMessage("❌ Proxy listesi yüklenemedi");
                    UseProxySwitch.IsToggled = false;
                    useProxy = false;
                }
            }
            catch (Exception ex)
            {
                LogProxyMessage($"❌ Proxy başlatma hatası: {ex.Message}");
                UseProxySwitch.IsToggled = false;
                useProxy = false;
            }
        }

        private void InitializeHttpClient()
        {
            var handler = useProxy && proxyService.CurrentProxy != null
                ? proxyService.CreateProxyHandler(proxyService.CurrentProxy)
                : new HttpClientHandler();

            httpClient?.Dispose();
            httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(currentTimeout)
            };

            SetupHttpClientHeaders();
        }

        private void SetupHttpClientHeaders()
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Android 12; Mobile; rv:109.0) Gecko/109.0 Firefox/109.0");
            httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        }

        // PROXY YÖNETİMİ
        private async void OnUseProxySwitchToggled(object sender, ToggledEventArgs e)
        {
            useProxy = e.Value;
            proxyService.IsProxyEnabled = useProxy;

            if (useProxy)
            {
                await SetBestProxy();
                ProxyModeSection.IsVisible = true;
                TestProxyButton.IsVisible = true;
                ProxyLogFrame.IsVisible = true;
            }
            else
            {
                proxyService.DisableProxy();
                ProxyModeSection.IsVisible = false;
                TestProxyButton.IsVisible = false;
                ProxyLogFrame.IsVisible = false;
            }

            UpdateProxyUI();
            InitializeHttpClient(); // HttpClient'ı yeniden başlat
        }

        private async Task SetBestProxy()
        {
            try
            {
                ProxyServer selectedProxy = null;

                switch (proxyMode)
                {
                    case "🚀 Otomatik En İyi":
                        selectedProxy = proxyService.GetBestProxy();
                        break;
                    case "🎲 Rastgele":
                        selectedProxy = proxyService.GetRandomProxy();
                        break;
                    case "🔄 Sıralı Rotasyon":
                        selectedProxy = proxyService.GetNextProxy();
                        break;
                    case "🇺🇸 Sadece ABD":
                        selectedProxy = proxyService.GetProxyByCountry("US");
                        break;
                    case "🇨🇦 Sadece Kanada":
                        selectedProxy = proxyService.GetProxyByCountry("CA");
                        break;
                    case "🇦🇹 Sadece Avusturya":
                        selectedProxy = proxyService.GetProxyByCountry("AT");
                        break;
                    case "🇧🇪 Sadece Belçika":
                        selectedProxy = proxyService.GetProxyByCountry("BE");
                        break;
                    case "🇧🇷 Sadece Brezilya":
                        selectedProxy = proxyService.GetProxyByCountry("BR");
                        break;
                    case "🇫🇷 Sadece Fransa":
                        selectedProxy = proxyService.GetProxyByCountry("FR");
                        break;
                }

                if (selectedProxy != null)
                {
                    proxyService.SetCurrentProxy(selectedProxy);
                    LogProxyMessage($"🌐 Proxy aktif: {selectedProxy.Country.GetCountryEmoji()} {selectedProxy.AliasName} - Load: {selectedProxy.Load.GetLoadDescription()}");
                    requestsSinceProxyChange = 0;
                }
                else
                {
                    LogProxyMessage("❌ Uygun proxy bulunamadı");
                }

                UpdateProxyUI();
            }
            catch (Exception ex)
            {
                LogProxyMessage($"❌ Proxy seçim hatası: {ex.Message}");
            }
        }

        private async void OnChangeProxyClicked(object sender, EventArgs e)
        {
            await AnimateButtonClick(ChangeProxyButton);

            if (useProxy)
            {
                await SetBestProxy();
                InitializeHttpClient(); // HttpClient'ı yeniden başlat
            }
        }

        private async void OnTestProxyClicked(object sender, EventArgs e)
        {
            await AnimateButtonClick(TestProxyButton);

            if (proxyService.CurrentProxy == null)
            {
                await DisplayAlert("⚠️ Uyarı", "Test edilecek proxy seçili değil!", "Tamam");
                return;
            }

            TestProxyButton.IsEnabled = false;
            TestProxyButton.Text = "🧪 Test Ediliyor...";

            try
            {
                bool testResult = await proxyService.TestProxy(proxyService.CurrentProxy, 10);

                if (testResult)
                {
                    LogProxyMessage($"✅ Proxy test başarılı: {proxyService.CurrentProxy.AliasName}");
                    await DisplayAlert("✅ Test Başarılı",
                        $"Proxy çalışıyor!\n{proxyService.CurrentProxy.Country.GetCountryEmoji()} {proxyService.CurrentProxy.AliasName}",
                        "Tamam");
                }
                else
                {
                    LogProxyMessage($"❌ Proxy test başarısız: {proxyService.CurrentProxy.AliasName}");
                    await DisplayAlert("❌ Test Başarısız",
                        $"Proxy çalışmıyor!\n{proxyService.CurrentProxy.AliasName}\n\nBaşka proxy deneyecek misiniz?",
                        "Tamam");

                    // Başka proxy dene
                    await SetBestProxy();
                }
            }
            catch (Exception ex)
            {
                LogProxyMessage($"❌ Test hatası: {ex.Message}");
                await DisplayAlert("❌ Test Hatası", ex.Message, "Tamam");
            }
            finally
            {
                TestProxyButton.IsEnabled = true;
                TestProxyButton.Text = "🧪 Proxy Test Et";
            }
        }

        private void UpdateProxyUI()
        {
            if (useProxy && proxyService.CurrentProxy != null)
            {
                var proxy = proxyService.CurrentProxy;

                ActiveProxyFrame.IsVisible = true;
                ProxyStatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                ActiveProxyNameLabel.Text = $"🟢 {proxy.Country.GetCountryEmoji()} {proxy.AliasName}";
                ActiveProxyDetailsLabel.Text = $"{proxy.Host}:{proxy.Port} • {proxy.Load.GetLoadDescription()}";
            }
            else if (useProxy)
            {
                ActiveProxyFrame.IsVisible = true;
                ProxyStatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                ActiveProxyNameLabel.Text = "🟠 Proxy Aranıyor...";
                ActiveProxyDetailsLabel.Text = "En uygun proxy seçiliyor";
            }
            else
            {
                ActiveProxyFrame.IsVisible = true;
                ProxyStatusIndicator.Fill = new SolidColorBrush(Colors.Gray);
                ActiveProxyNameLabel.Text = "🔴 Proxy Kapalı";
                ActiveProxyDetailsLabel.Text = "Doğrudan bağlantı kullanılıyor";
            }
        }

        private void LogProxyMessage(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}";

                if (string.IsNullOrEmpty(ProxyLogLabel.Text))
                {
                    ProxyLogLabel.Text = logEntry;
                }
                else
                {
                    var lines = ProxyLogLabel.Text.Split('\n').ToList();
                    lines.Add(logEntry);

                    // Sadece son 10 satırı tut
                    if (lines.Count > 10)
                    {
                        lines = lines.TakeLast(10).ToList();
                    }

                    ProxyLogLabel.Text = string.Join("\n", lines);
                }
            });
        }

        // WELCOME ANİMASYONU
        private async Task PlayWelcomeAnimation()
        {
            this.Opacity = 0;
            this.TranslationY = 50;

            await Task.WhenAll(
                this.FadeTo(1, 800, Easing.CubicOut),
                this.TranslateTo(0, 0, 800, Easing.CubicOut)
            );
        }

        // GERİ BUTONU
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await AnimateButtonClick(BackButton);

            // İşlem çalışıyorsa uyar
            if (isRunning)
            {
                bool shouldStop = await DisplayAlert("⚠️ Uyarı",
                    "İşlem devam ediyor. Çıkmak istediğinizden emin misiniz?",
                    "Evet", "Hayır");

                if (!shouldStop) return;

                // İşlemi durdur
                OnStopClicked(sender, e);
            }

            // Ana sayfaya dön
            await Shell.Current.GoToAsync("..");
        }

        // URL YÖNETİMİ - EKLEME
        private async void OnAddUrlClicked(object sender, EventArgs e)
        {
            await AnimateButtonClick(AddUrlButton);

            string url = UrlEntry.Text?.Trim();

            if (string.IsNullOrEmpty(url) || url == "https://")
            {
                await DisplayAlert("⚠️ Uyarı", "Lütfen geçerli bir URL girin!", "Tamam");
                return;
            }

            if (!IsValidUrl(url))
            {
                await DisplayAlert("⚠️ Uyarı", "Geçersiz URL formatı!\nÖrnek: https://example.com", "Tamam");
                return;
            }

            if (urlList.Contains(url))
            {
                await DisplayAlert("⚠️ Uyarı", "Bu URL zaten listede mevcut!", "Tamam");
                return;
            }

            // URL'yi listeye ekle
            urlList.Add(url);
            UrlEntry.Text = "https://";
            UpdateUrlCount();

            await DisplayAlert("✅ Başarılı", $"URL başarıyla eklendi!\nToplam: {urlList.Count}", "Tamam");
        }

        // URL GİRİŞ ENTER TUŞU
        private void OnUrlEntryCompleted(object sender, EventArgs e)
        {
            OnAddUrlClicked(sender, e);
        }

        // URL SİLME
        private async void OnRemoveUrlClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string url)
            {
                bool shouldRemove = await DisplayAlert("🗑️ Sil",
                    $"Bu URL'yi silmek istediğinizden emin misiniz?\n\n{url}",
                    "Evet", "Hayır");

                if (shouldRemove)
                {
                    urlList.Remove(url);
                    UpdateUrlCount();
                    await DisplayAlert("✅ Silindi", "URL başarıyla silindi!", "Tamam");
                }
            }
        }

        // DOSYADAN URL YÜKLEME
        private async void OnLoadFileClicked(object sender, EventArgs e)
        {
            await AnimateButtonClick(LoadFileButton);

            try
            {
                var result = await FilePicker.Default.PickAsync();

                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    using var reader = new StreamReader(stream);

                    var content = await reader.ReadToEndAsync();
                    var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    int addedCount = 0;
                    foreach (var line in lines)
                    {
                        var url = line.Trim();
                        if (IsValidUrl(url) && !urlList.Contains(url))
                        {
                            urlList.Add(url);
                            addedCount++;
                        }
                    }

                    UpdateUrlCount();
                    await DisplayAlert("📁 Dosya Yüklendi",
                        $"{addedCount} yeni URL eklendi!\nToplam: {urlList.Count}", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"Dosya yüklenirken hata oluştu:\n{ex.Message}", "Tamam");
            }
        }

        // URL LİSTESİNİ TEMİZLE
        private async void OnClearUrlsClicked(object sender, EventArgs e)
        {
            await AnimateButtonClick(ClearUrlsButton);

            if (urlList.Count == 0)
            {
                await DisplayAlert("ℹ️ Bilgi", "Temizlenecek URL bulunamadı!", "Tamam");
                return;
            }

            bool shouldClear = await DisplayAlert("🗑️ Temizle",
                $"{urlList.Count} URL'yi silmek istediğinizden emin misiniz?",
                "Evet", "Hayır");

            if (shouldClear)
            {
                urlList.Clear();
                UpdateUrlCount();
                await DisplayAlert("✅ Temizlendi", "Tüm URL'ler silindi!", "Tamam");
            }
        }

        // HIZ DEĞİŞİKLİĞİ
        private void OnSpeedChanged(object sender, ValueChangedEventArgs e)
        {
            currentSpeed = (int)e.NewValue;
            UpdateSpeedLabel();
        }

        // TIMEOUT DEĞİŞİKLİĞİ
        private void OnTimeoutChanged(object sender, ValueChangedEventArgs e)
        {
            currentTimeout = (int)e.NewValue;
            UpdateTimeoutLabel();

            if (httpClient != null)
            {
                httpClient.Timeout = TimeSpan.FromSeconds(currentTimeout);
            }
        }

        // PROXY MODE DEĞİŞİKLİĞİ
        private async void OnProxyModeChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedItem != null)
            {
                proxyMode = picker.SelectedItem.ToString();

                if (useProxy)
                {
                    await SetBestProxy();
                    InitializeHttpClient();
                }
            }
        }

        // BOT BAŞLAT
        private async void OnStartClicked(object sender, EventArgs e)
        {
            await AnimateButtonClick(StartButton);

            // FORM VALİDASYONU
            if (!ValidateForm())
                return;

            if (urlList.Count == 0)
            {
                await DisplayAlert("⚠️ Uyarı", "Lütfen önce URL listesi ekleyin!", "Tamam");
                return;
            }

            // BOT'U BAŞLAT
            await StartBot();
        }

        // BOT DURDUR
        private async void OnStopClicked(object sender, EventArgs e)
        {
            await AnimateButtonClick(StopButton);
            StopBot();
        }

        // BOT BAŞLATMA LOGİC
        private async Task StartBot()
        {
            isRunning = true;
            cancellationTokenSource = new CancellationTokenSource();

            // UI GÜNCELLE
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusLabel.Text = "🔄 Çalışıyor";
            StatusLabel.TextColor = Colors.Orange;

            // İSTATİSTİKLERİ SIFIRLA
            ResetStats();

            try
            {
                var proxyInfo = useProxy && proxyService.CurrentProxy != null
                    ? $"\nProxy: {proxyService.CurrentProxy.Country.GetCountryEmoji()} {proxyService.CurrentProxy.AliasName}"
                    : "\nProxy: Kapalı (Doğrudan bağlantı)";

                await DisplayAlert("🚀 Bot Başlatıldı",
                    $"Toplam {urlList.Count} URL işlenecek\n" +
                    $"Hız: {GetSpeedText(currentSpeed)}\n" +
                    $"Timeout: {currentTimeout} saniye" +
                    proxyInfo, "Tamam");

                // PARALELİ İŞLEMLER
                var semaphore = new SemaphoreSlim(GetMaxParallel(), GetMaxParallel());
                var tasks = urlList.Select(url => ProcessUrlWithSemaphore(url, semaphore, cancellationTokenSource.Token));

                await Task.WhenAll(tasks);

                if (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await DisplayAlert("🎉 Tamamlandı!",
                        $"İşlem başarıyla tamamlandı!\n\n" +
                        $"📊 Toplam: {totalProcessed}\n" +
                        $"✅ Başarılı: {successCount}\n" +
                        $"❌ Başarısız: {failedCount}\n" +
                        $"🎯 Başarı Oranı: %{GetSuccessRate():F1}", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"İşlem sırasında hata oluştu:\n{ex.Message}", "Tamam");
            }
            finally
            {
                StopBot();
            }
        }

        // URL İŞLEME (SEMAPHORE İLE)
        private async Task ProcessUrlWithSemaphore(string url, SemaphoreSlim semaphore, CancellationToken token)
        {
            await semaphore.WaitAsync(token);

            try
            {
                await ProcessSingleUrl(url, token);
            }
            finally
            {
                semaphore.Release();
            }
        }

        // TEK URL İŞLEME
        private async Task ProcessSingleUrl(string url, CancellationToken token)
        {
            try
            {
                // PROXY ROTASYONU KONTROL ET
                if (useProxy && autoProxyRotation && requestsSinceProxyChange >= MAX_REQUESTS_PER_PROXY)
                {
                    await SetBestProxy();
                    InitializeHttpClient();
                }

                // HTML İNDİR
                var html = await httpClient.GetStringAsync(url, token);

                if (string.IsNullOrEmpty(html))
                {
                    IncrementFailed();
                    return;
                }

                // FORM VE CAPTCHA BUL
                var formAction = await FindFormAction(html, url);
                if (string.IsNullOrEmpty(formAction))
                {
                    IncrementFailed();
                    return;
                }

                var captcha = SolveCaptcha(html);
                var formData = PrepareFormData(captcha);

                // FORM GÖNDER
                bool success = await SubmitForm(formAction, formData, token);

                if (success)
                {
                    IncrementSuccess();
                    if (useProxy && proxyService.CurrentProxy != null)
                    {
                        LogProxyMessage($"✅ Başarılı: {url} via {proxyService.CurrentProxy.AliasName}");
                    }
                }
                else
                {
                    IncrementFailed();
                    if (useProxy && proxyService.CurrentProxy != null)
                    {
                        LogProxyMessage($"❌ Başarısız: {url} via {proxyService.CurrentProxy.AliasName}");
                    }
                }

                // İSTEK SAYACINI ARTIR
                requestsSinceProxyChange++;

                // GECİKME
                await Task.Delay(GetDelay(), token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                IncrementFailed();
                LogProxyMessage($"❌ Hata: {url} - {ex.Message}");
            }
        }

        // FORM ACTION BULMA
        private async Task<string> FindFormAction(string html, string baseUrl)
        {
            try
            {
                // AngleSharp ile parse
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(html);

                var forms = document.QuerySelectorAll("form");
                foreach (var form in forms)
                {
                    // Form içinde input alanları var mı kontrol et
                    var hasNameField = form.QuerySelector("input[name*='name'], input[name*='ad'], input[name*='isim']") != null;
                    var hasEmailField = form.QuerySelector("input[name*='email'], input[name*='mail']") != null;
                    var hasMessageField = form.QuerySelector("textarea, input[name*='message'], input[name*='mesaj']") != null;

                    if (hasNameField && hasEmailField && hasMessageField)
                    {
                        var action = form.GetAttribute("action") ?? "";

                        if (string.IsNullOrEmpty(action) || action == "#")
                            return baseUrl;

                        if (action.StartsWith("http"))
                            return action;

                        var uri = new Uri(baseUrl);
                        if (action.StartsWith("/"))
                            return $"{uri.Scheme}://{uri.Host}{action}";

                        return $"{baseUrl.TrimEnd('/')}/{action.TrimStart('/')}";
                    }
                }
            }
            catch
            {
                // Parse hatası durumunda HtmlAgilityPack dene
                try
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var forms = doc.DocumentNode.SelectNodes("//form");
                    if (forms != null)
                    {
                        foreach (var form in forms)
                        {
                            var action = form.GetAttributeValue("action", "");
                            if (!string.IsNullOrEmpty(action))
                                return action.StartsWith("http") ? action : baseUrl;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        // CAPTCHA ÇÖZME
        private string SolveCaptcha(string html)
        {
            try
            {
                foreach (var pattern in CaptchaPatterns)
                {
                    var match = pattern.Match(html);
                    if (match.Success)
                    {
                        var captchaText = Regex.Replace(match.Value, @"<[^>]*>", "").Trim();
                        if (!string.IsNullOrEmpty(captchaText))
                            return captchaText;
                    }
                }
            }
            catch { }

            return "test";
        }

        // FORM DATA HAZIRLAMA
        private Dictionary<string, string> PrepareFormData(string captcha)
        {
            return new Dictionary<string, string>
            {
                ["name"] = NameEntry.Text?.Trim() ?? "",
                ["ad"] = NameEntry.Text?.Trim() ?? "",
                ["isim"] = NameEntry.Text?.Trim() ?? "",
                ["email"] = EmailEntry.Text?.Trim() ?? "",
                ["mail"] = EmailEntry.Text?.Trim() ?? "",
                ["eposta"] = EmailEntry.Text?.Trim() ?? "",
                ["location"] = LocationEntry.Text?.Trim() ?? "",
                ["yer"] = LocationEntry.Text?.Trim() ?? "",
                ["sehir"] = LocationEntry.Text?.Trim() ?? "",
                ["web"] = WebsiteEntry.Text?.Trim() ?? "",
                ["website"] = WebsiteEntry.Text?.Trim() ?? "",
                ["url"] = WebsiteEntry.Text?.Trim() ?? "",
                ["message"] = MessageEditor.Text?.Trim() ?? "",
                ["mesaj"] = MessageEditor.Text?.Trim() ?? "",
                ["yorum"] = MessageEditor.Text?.Trim() ?? "",
                ["captcha"] = captcha,
                ["seccode"] = captcha,
                ["security"] = captcha,
                ["kod"] = captcha
            };
        }

        // FORM GÖNDERME
        private async Task<bool> SubmitForm(string actionUrl, Dictionary<string, string> formData, CancellationToken token)
        {
            try
            {
                using var content = new FormUrlEncodedContent(formData);
                var response = await httpClient.PostAsync(actionUrl, content, token);

                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    var successKeywords = new[] { "teşekkür", "thank", "success", "başarı", "kaydedildi", "saved" };

                    return successKeywords.Any(keyword =>
                        responseText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch { }

            return false;
        }

        // BOT DURDURMA
        private void StopBot()
        {
            isRunning = false;
            cancellationTokenSource?.Cancel();

            // UI GÜNCELLE
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusLabel.Text = "🟢 Hazır";
            StatusLabel.TextColor = Colors.Green;
        }

        // HELPER METODLAR
        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(NameEntry.Text))
            {
                DisplayAlert("⚠️ Uyarı", "Ad Soyad alanı gerekli!", "Tamam");
                return false;
            }

            if (string.IsNullOrWhiteSpace(EmailEntry.Text))
            {
                DisplayAlert("⚠️ Uyarı", "E-mail alanı gerekli!", "Tamam");
                return false;
            }

            return true;
        }

        private void UpdateUrlCount()
        {
            UrlCountLabel.Text = $"{urlList.Count} URL";
        }

        private void UpdateSpeedLabel()
        {
            SpeedValueLabel.Text = $"{GetSpeedText(currentSpeed)} ({currentSpeed})";
        }

        private void UpdateTimeoutLabel()
        {
            TimeoutValueLabel.Text = $"{currentTimeout} saniye";
        }

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

        private int GetMaxParallel()
        {
            return currentSpeed switch
            {
                1 or 2 or 3 => 1,
                4 or 5 or 6 => 2,
                7 or 8 => 3,
                _ => 4
            };
        }

        private int GetDelay()
        {
            return currentSpeed switch
            {
                1 => 5000,
                2 => 4000,
                3 => 3000,
                4 => 2500,
                5 => 2000,
                6 => 1500,
                7 => 1000,
                8 => 800,
                9 => 500,
                10 => 300,
                _ => 2000
            };
        }

        private void ResetStats()
        {
            totalProcessed = 0;
            successCount = 0;
            failedCount = 0;
            requestsSinceProxyChange = 0;
            UpdateStatsUI();
        }

        private void IncrementSuccess()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                successCount++;
                totalProcessed++;
                UpdateStatsUI();
            });
        }

        private void IncrementFailed()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                failedCount++;
                totalProcessed++;
                UpdateStatsUI();
            });
        }

        private void UpdateStatsUI()
        {
            TotalProcessedLabel.Text = totalProcessed.ToString();
            SuccessCountLabel.Text = successCount.ToString();
            FailedCountLabel.Text = failedCount.ToString();
            SuccessRateLabel.Text = $"{GetSuccessRate():F1}%";

            // Progress bar güncelle
            if (urlList.Count > 0)
            {
                ProcessProgressBar.Progress = (double)totalProcessed / urlList.Count;
            }
        }

        private double GetSuccessRate()
        {
            return totalProcessed > 0 ? (double)successCount / totalProcessed * 100 : 0;
        }

        // RESULTS MANAGEMENT
        private async void OnClearResultsClicked(object sender, EventArgs e)
        {
            await AnimateButtonClick(ClearResultsButton);

            if (string.IsNullOrEmpty(ResultsLabel.Text))
            {
                await DisplayAlert("ℹ️ Bilgi", "Temizlenecek sonuç bulunamadı!", "Tamam");
                return;
            }

            bool shouldClear = await DisplayAlert("🗑️ Temizle",
                "Tüm sonuçları silmek istediğinizden emin misiniz?",
                "Evet", "Hayır");

            if (shouldClear)
            {
                ResultsLabel.Text = "";
                ResultsFrame.IsVisible = false;
                await DisplayAlert("✅ Temizlendi", "Sonuçlar temizlendi!", "Tamam");
            }
        }

        // ENHANCED PROXY FEATURES
        private async Task CheckProxyHealth()
        {
            if (!useProxy || proxyService.CurrentProxy == null) return;

            try
            {
                bool isHealthy = await proxyService.TestProxy(proxyService.CurrentProxy, 5);

                if (!isHealthy)
                {
                    LogProxyMessage($"⚠️ Proxy sağlıksız: {proxyService.CurrentProxy.AliasName}, değiştiriliyor...");
                    await SetBestProxy();
                    InitializeHttpClient();
                }
            }
            catch (Exception ex)
            {
                LogProxyMessage($"❌ Proxy sağlık kontrolü hatası: {ex.Message}");
            }
        }

        private async Task RotateProxyIfNeeded()
        {
            if (!useProxy || !autoProxyRotation) return;

            if (requestsSinceProxyChange >= MAX_REQUESTS_PER_PROXY)
            {
                LogProxyMessage($"🔄 Proxy rotasyonu: {requestsSinceProxyChange} istek tamamlandı");
                await SetBestProxy();
                InitializeHttpClient();
            }
        }

        // ADVANCED ERROR HANDLING
        private async Task HandleProxyError(Exception ex, string url)
        {
            LogProxyMessage($"❌ Proxy hatası: {ex.Message}");

            if (useProxy && proxyService.CurrentProxy != null)
            {
                // Proxy'yi deaktif et
                proxyService.CurrentProxy.IsActive = false;
                LogProxyMessage($"🚫 Proxy deaktif edildi: {proxyService.CurrentProxy.AliasName}");

                // Yeni proxy seç
                await SetBestProxy();

                if (proxyService.CurrentProxy != null)
                {
                    InitializeHttpClient();
                    LogProxyMessage($"🔄 Yeni proxy aktif: {proxyService.CurrentProxy.AliasName}");
                }
                else
                {
                    LogProxyMessage("⚠️ Kullanılabilir proxy bulunamadı, doğrudan bağlantıya geçiliyor");
                    useProxy = false;
                    UseProxySwitch.IsToggled = false;
                    InitializeHttpClient();
                }
            }
        }

        // PROXY STATISTICS UPDATE
        private void UpdateProxyStatistics()
        {
            if (proxyService != null)
            {
                var stats = proxyService.GetProxyStats();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProxyCountLabel.Text = stats.ActiveProxies.ToString();
                    ProxyLoadLabel.Text = $"{stats.AverageLoad}%";
                    ProxyCountryLabel.Text = stats.Countries.ToString();
                });
            }
        }

        // AUTO PROXY SWITCH EVENT HANDLER
        private void OnAutoProxySwitchToggled(object sender, ToggledEventArgs e)
        {
            autoProxyRotation = e.Value;
            LogProxyMessage($"🔄 Otomatik proxy rotasyonu: {(autoProxyRotation ? "Açık" : "Kapalı")}");
        }

        // ENHANCED LOGGING
        private void LogResult(string message, bool isSuccess = true)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var icon = isSuccess ? "✅" : "❌";
                var logEntry = $"[{timestamp}] {icon} {message}";

                if (string.IsNullOrEmpty(ResultsLabel.Text))
                {
                    ResultsLabel.Text = logEntry;
                }
                else
                {
                    var lines = ResultsLabel.Text.Split('\n').ToList();
                    lines.Add(logEntry);

                    // Sadece son 50 satırı tut
                    if (lines.Count > 50)
                    {
                        lines = lines.TakeLast(50).ToList();
                    }

                    ResultsLabel.Text = string.Join("\n", lines);
                }

                ResultsFrame.IsVisible = !string.IsNullOrEmpty(ResultsLabel.Text);
            });
        }

        // BUTTON CLICK ANİMASYONU
        private async Task AnimateButtonClick(Button button)
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

        // MEMORY CLEANUP
        private void CleanupResources()
        {
            try
            {
                httpClient?.Dispose();
                cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        // SAYFA KAPANIRKEN CLEANUP
        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            if (isRunning)
            {
                StopBot();
            }

            CleanupResources();
        }

        // DESTRUCTOR
        ~BacklinkPage()
        {
            CleanupResources();
        }
    }
}