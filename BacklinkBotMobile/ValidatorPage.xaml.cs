using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using HtmlAgilityPack;
using System.Diagnostics;
using System.Text;

namespace BacklinkBotMobile
{
    public partial class ValidatorPage : ContentPage
    {
        // COLLECTIONS VE VERİ YÖNETİMİ
        private ObservableCollection<TestResult> testResults;
        private ObservableCollection<TestResult> filteredResults;
        private CancellationTokenSource cancellationTokenSource;
        private bool isTestRunning = false;

        // İSTATİSTİK VERİLERİ
        private int totalTested = 0;
        private int activeLinks = 0;
        private int commentAreaLinks = 0;
        private int deadLinks = 0;
        private List<long> responseTimes = new List<long>();

        // TEST AYARLARI
        private int testTimeout = 10;
        private bool httpStatusEnabled = true;
        private bool formDetectionEnabled = true;
        private bool responseTimeEnabled = true;
        private bool contentAnalysisEnabled = false;

        // HTTP CLIENT - SÜPER HIZLI
        private static readonly HttpClient httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // FORM DETECTION PATTERNS
        private static readonly Regex[] FormPatterns = {
            new Regex(@"<form[^>]*>[\s\S]*?<textarea[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"<form[^>]*>[\s\S]*?<input[^>]*type=[""']text[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"<textarea[^>]*name=[""']?(comment|yorum|mesaj|message)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"<input[^>]*name=[""']?(comment|yorum|mesaj|message|name|email)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        public ValidatorPage()
        {
            InitializeComponent();
            InitializePage();
        }

        private async void InitializePage()
        {
            // COLLECTIONS INITIALIZE
            testResults = new ObservableCollection<TestResult>();
            filteredResults = new ObservableCollection<TestResult>();
            ResultsCollectionView.ItemsSource = filteredResults;

            // HTTP CLIENT SETUP
            SetupHttpClient();

            // WELCOME ANIMATION
            await PlayWelcomeAnimation();

            // INITIAL UPDATES
            UpdateLabels();
        }

        private void SetupHttpClient()
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Android 12; Mobile; rv:109.0) Gecko/109.0 Firefox/109.0");
            httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
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

        // GERİ BUTONU
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await AnimateButton(BackButton);

            if (isTestRunning)
            {
                bool shouldStop = await DisplayAlert("⚠️ Uyarı",
                    "Test devam ediyor. Çıkmak istediğinizden emin misiniz?",
                    "Evet", "Hayır");

                if (!shouldStop) return;

                StopTesting();
            }

            await Shell.Current.GoToAsync("..");
        }

        // TEK URL TEST
        private async void OnTestSingleClicked(object sender, EventArgs e)
        {
            await AnimateButton(TestSingleButton);

            string url = SingleUrlEntry.Text?.Trim();

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

            await TestSingleUrl(url);
        }

        // TEK URL ENTRY ENTER
        private void OnSingleUrlEntryCompleted(object sender, EventArgs e)
        {
            OnTestSingleClicked(sender, e);
        }

        // TOPLU URL TEST
        private async void OnTestBulkClicked(object sender, EventArgs e)
        {
            await AnimateButton(TestBulkButton);

            string bulkText = BulkUrlEditor.Text?.Trim();

            if (string.IsNullOrEmpty(bulkText))
            {
                await DisplayAlert("⚠️ Uyarı", "Lütfen test edilecek URL'leri girin!", "Tamam");
                return;
            }

            var urls = bulkText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(line => line.Trim())
                              .Where(IsValidUrl)
                              .ToList();

            if (urls.Count == 0)
            {
                await DisplayAlert("⚠️ Uyarı", "Geçerli URL bulunamadı!", "Tamam");
                return;
            }

            await TestBulkUrls(urls);
        }

        // DOSYADAN YÜKLE
        private async void OnLoadFileClicked(object sender, EventArgs e)
        {
            await AnimateButton(LoadFileButton);

            try
            {
                var result = await FilePicker.Default.PickAsync();

                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    using var reader = new StreamReader(stream);

                    var content = await reader.ReadToEndAsync();
                    var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    var validUrls = lines.Select(line => line.Trim())
                                        .Where(IsValidUrl)
                                        .ToList();

                    if (validUrls.Count > 0)
                    {
                        BulkUrlEditor.Text = string.Join("\n", validUrls);
                        await DisplayAlert("📁 Dosya Yüklendi",
                            $"{validUrls.Count} geçerli URL yüklendi!", "Tamam");
                    }
                    else
                    {
                        await DisplayAlert("⚠️ Uyarı", "Dosyada geçerli URL bulunamadı!", "Tamam");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"Dosya yüklenirken hata oluştu:\n{ex.Message}", "Tamam");
            }
        }

        // TEMİZLE
        private async void OnClearAllClicked(object sender, EventArgs e)
        {
            await AnimateButton(ClearAllButton);

            bool shouldClear = await DisplayAlert("🗑️ Temizle",
                "Tüm URL'leri temizlemek istediğinizden emin misiniz?",
                "Evet", "Hayır");

            if (shouldClear)
            {
                SingleUrlEntry.Text = "https://";
                BulkUrlEditor.Text = "";
                await DisplayAlert("✅ Temizlendi", "Tüm URL'ler temizlendi!", "Tamam");
            }
        }

        // TEST TIMEOUT DEĞİŞİMİ
        private void OnTestTimeoutChanged(object sender, ValueChangedEventArgs e)
        {
            testTimeout = (int)e.NewValue;
            TestTimeoutLabel.Text = $"{testTimeout} saniye";
            httpClient.Timeout = TimeSpan.FromSeconds(testTimeout);
        }

        // TEST DURDUR
        private async void OnStopTestClicked(object sender, EventArgs e)
        {
            await AnimateButton(StopTestButton);
            StopTesting();
        }

        // FİLTRE
        private async void OnFilterClicked(object sender, EventArgs e)
        {
            await AnimateButton(FilterButton);

            string action = await DisplayActionSheet("🔍 Filtrele", "İptal", null,
                "Tümü", "✅ Sadece Aktif", "💬 Yorum Alanı Var", "❌ Sadece Ölü");

            ApplyFilter(action);
        }

        // SIRALA
        private async void OnSortClicked(object sender, EventArgs e)
        {
            await AnimateButton(SortButton);

            string action = await DisplayActionSheet("📊 Sırala", "İptal", null,
                "URL'ye Göre", "Duruma Göre", "Hıza Göre", "Son Eklenen");

            ApplySort(action);
        }

        // RAPOR
        private async void OnReportClicked(object sender, EventArgs e)
        {
            await AnimateButton(ReportButton);
            await GenerateDetailedReport();
        }

        // SONUÇLARI KAYDET
        private async void OnSaveResultsClicked(object sender, EventArgs e)
        {
            await AnimateButton(SaveResultsButton);
            await SaveResults();
        }

        // SONUÇLARI TEMİZLE
        private async void OnClearResultsClicked(object sender, EventArgs e)
        {
            await AnimateButton(ClearResultsButton);

            bool shouldClear = await DisplayAlert("🗑️ Temizle",
                "Tüm test sonuçlarını temizlemek istediğinizden emin misiniz?",
                "Evet", "Hayır");

            if (shouldClear)
            {
                ClearAllResults();
                await DisplayAlert("✅ Temizlendi", "Tüm sonuçlar temizlendi!", "Tamam");
            }
        }

        // TEK URL TEST LOGIC
        private async Task TestSingleUrl(string url)
        {
            StatusLabel.Text = "🔄 Test ediliyor...";
            StopTestButton.IsEnabled = true;

            try
            {
                var result = await ValidateUrl(url);
                AddTestResult(result);

                StatusLabel.Text = $"✅ Test tamamlandı: {result.StatusIcon}";
                await DisplayAlert("🧪 Test Tamamlandı",
                    $"URL: {url}\n" +
                    $"Durum: {result.Status}\n" +
                    $"Yanıt Süresi: {result.ResponseTime}\n" +
                    $"Yorum Alanı: {(result.HasCommentArea ? "✅ Var" : "❌ Yok")}", "Tamam");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "❌ Test hatası";
                await DisplayAlert("❌ Hata", $"Test sırasında hata oluştu:\n{ex.Message}", "Tamam");
            }
            finally
            {
                StopTestButton.IsEnabled = false;
            }
        }

        // TOPLU URL TEST LOGIC
        private async Task TestBulkUrls(List<string> urls)
        {
            isTestRunning = true;
            cancellationTokenSource = new CancellationTokenSource();

            StopTestButton.IsEnabled = true;
            TestProgressBar.Progress = 0;
            StatusLabel.Text = $"🚀 {urls.Count} URL test ediliyor...";

            try
            {
                var semaphore = new SemaphoreSlim(3, 3); // 3 paralel test
                var tasks = urls.Select(async (url, index) =>
                {
                    await semaphore.WaitAsync(cancellationTokenSource.Token);
                    try
                    {
                        var result = await ValidateUrl(url);

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            AddTestResult(result);
                            TestProgressBar.Progress = (double)(index + 1) / urls.Count;
                            StatusLabel.Text = $"🔄 {index + 1}/{urls.Count} - {result.Url}";
                        });

                        await Task.Delay(200, cancellationTokenSource.Token); // Rate limiting
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);

                if (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusLabel.Text = $"🎉 {urls.Count} URL test tamamlandı!";
                    await DisplayAlert("🎉 Test Tamamlandı",
                        $"Toplam: {urls.Count} URL\n" +
                        $"✅ Aktif: {activeLinks}\n" +
                        $"💬 Yorum Alanı: {commentAreaLinks}\n" +
                        $"❌ Ölü: {deadLinks}\n" +
                        $"🎯 Başarı Oranı: %{GetSuccessRate():F1}", "Tamam");
                }
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Text = "⏹️ Test durduruldu";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "❌ Test hatası";
                await DisplayAlert("❌ Hata", $"Toplu test hatası:\n{ex.Message}", "Tamam");
            }
            finally
            {
                StopTesting();
            }
        }

        // URL VALİDASYON LOGIC
        private async Task<TestResult> ValidateUrl(string url)
        {
            var result = new TestResult { Url = url };
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    using (var response = await httpClient.SendAsync(request, cancellationTokenSource?.Token ?? CancellationToken.None))
                    {
                        stopwatch.Stop();
                        result.ResponseTime = $"{stopwatch.ElapsedMilliseconds}ms";
                        result.StatusCode = $"HTTP {(int)response.StatusCode}";

                        if (response.IsSuccessStatusCode)
                        {
                            result.IsActive = true;
                            result.Status = "Aktif";
                            result.StatusIcon = "✅";
                            result.StatusColor = Colors.Green;

                            // FORM DETECTION
                            if (formDetectionEnabled)
                            {
                                try
                                {
                                    string content = await response.Content.ReadAsStringAsync();
                                    result.HasCommentArea = await DetectCommentArea(content);

                                    if (result.HasCommentArea)
                                    {
                                        result.Details = "Yorum alanı mevcut";
                                        commentAreaLinks++;
                                    }
                                    else
                                    {
                                        result.Details = "Yorum alanı bulunamadı";
                                    }
                                }
                                catch
                                {
                                    result.Details = "İçerik analizi yapılamadı";
                                }
                            }

                            activeLinks++;
                        }
                        else
                        {
                            result.IsActive = false;
                            result.Status = "Ölü";
                            result.StatusIcon = "❌";
                            result.StatusColor = Colors.Red;
                            result.Details = $"HTTP {response.StatusCode}";
                            deadLinks++;
                        }
                    }
                }

                responseTimes.Add(stopwatch.ElapsedMilliseconds);
            }
            catch (TaskCanceledException)
            {
                result.StatusIcon = "⏱️";
                result.Status = "Timeout";
                result.StatusColor = Colors.Orange;
                result.Details = $"Timeout ({testTimeout}s)";
                deadLinks++;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.StatusIcon = "💥";
                result.Status = "Hata";
                result.StatusColor = Colors.Red;
                result.ResponseTime = $"{stopwatch.ElapsedMilliseconds}ms";
                result.Details = ex.Message.Length > 50 ? ex.Message.Substring(0, 50) + "..." : ex.Message;
                deadLinks++;
            }

            totalTested++;
            return result;
        }

        // YORUM ALANI TESPİTİ
        private async Task<bool> DetectCommentArea(string htmlContent)
        {
            try
            {
                // REGEX PATTERN CHECK
                foreach (var pattern in FormPatterns)
                {
                    if (pattern.IsMatch(htmlContent))
                        return true;
                }

                // ANGLESHARP CHECK
                var parser = new HtmlParser();
                var document = await parser.ParseDocumentAsync(htmlContent);

                var forms = document.QuerySelectorAll("form");
                foreach (var form in forms)
                {
                    var hasNameField = form.QuerySelector("input[name*='name'], input[name*='ad']") != null;
                    var hasEmailField = form.QuerySelector("input[name*='email'], input[name*='mail']") != null;
                    var hasMessageField = form.QuerySelector("textarea, input[name*='message'], input[name*='mesaj']") != null;

                    if (hasNameField && hasEmailField && hasMessageField)
                        return true;
                }

                // HTMLAGILITYPACK FALLBACK
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                var formNodes = doc.DocumentNode.SelectNodes("//form");
                if (formNodes != null)
                {
                    foreach (var formNode in formNodes)
                    {
                        var inputs = formNode.SelectNodes(".//input | .//textarea");
                        if (inputs != null && inputs.Count >= 3)
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }

        // TEST RESULT EKLEME
        private void AddTestResult(TestResult result)
        {
            testResults.Add(result);
            filteredResults.Add(result);
            UpdateStats();
        }

        // STATİSTİKLERİ GÜNCELLE
        private void UpdateStats()
        {
            TotalTestedLabel.Text = totalTested.ToString();
            ActiveLinksLabel.Text = activeLinks.ToString();
            CommentAreaLabel.Text = commentAreaLinks.ToString();
            DeadLinksLabel.Text = deadLinks.ToString();

            if (responseTimes.Count > 0)
            {
                AvgSpeedLabel.Text = $"{responseTimes.Average():F0}ms";
            }

            SuccessRateLabel.Text = $"{GetSuccessRate():F1}%";
        }

        // FİLTRE UYGULA
        private void ApplyFilter(string filterType)
        {
            filteredResults.Clear();

            IEnumerable<TestResult> filtered;

            if (filterType == "✅ Sadece Aktif")
                filtered = testResults.Where(r => r.IsActive);
            else if (filterType == "💬 Yorum Alanı Var")
                filtered = testResults.Where(r => r.HasCommentArea);
            else if (filterType == "❌ Sadece Ölü")
                filtered = testResults.Where(r => !r.IsActive);
            else
                filtered = testResults;

            foreach (var result in filtered)
            {
                filteredResults.Add(result);
            }
        }

        // SIRALAMA UYGULA
        private void ApplySort(string sortType)
        {
            IEnumerable<TestResult> sorted;

            if (sortType == "URL'ye Göre")
                sorted = filteredResults.OrderBy(r => r.Url);
            else if (sortType == "Duruma Göre")
                sorted = filteredResults.OrderBy(r => r.Status);
            else if (sortType == "Hıza Göre")
                sorted = filteredResults.OrderBy(r => ParseResponseTime(r.ResponseTime));
            else if (sortType == "Son Eklenen")
                sorted = filteredResults.OrderByDescending(r => testResults.IndexOf(r));
            else
                sorted = filteredResults;

            var sortedList = sorted.ToList();
            filteredResults.Clear();

            foreach (var result in sortedList)
            {
                filteredResults.Add(result);
            }
        }

        // SONUÇLARI KAYDET
        private async Task SaveResults()
        {
            try
            {
                if (testResults.Count == 0)
                {
                    await DisplayAlert("ℹ️ Bilgi", "Kaydedilecek sonuç bulunamadı!", "Tamam");
                    return;
                }

                var fileName = $"LinkValidator_Results_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                var report = new StringBuilder();
                report.AppendLine("🔍 LINK VALIDATOR SONUÇLARI");
                report.AppendLine("=" + new string('=', 40));
                report.AppendLine($"📅 Tarih: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                report.AppendLine();

                report.AppendLine("📊 ÖZET:");
                report.AppendLine($"Toplam Test: {totalTested}");
                report.AppendLine($"✅ Aktif: {activeLinks}");
                report.AppendLine($"💬 Yorum Alanı: {commentAreaLinks}");
                report.AppendLine($"❌ Ölü: {deadLinks}");
                report.AppendLine($"🎯 Başarı Oranı: %{GetSuccessRate():F1}");
                report.AppendLine();

                report.AppendLine("📋 DETAYLAR:");
                foreach (var result in testResults)
                {
                    report.AppendLine($"{result.StatusIcon} {result.Url}");
                    report.AppendLine($"   Durum: {result.Status} | {result.StatusCode} | {result.ResponseTime}");
                    report.AppendLine($"   Detay: {result.Details}");
                    report.AppendLine();
                }

                await File.WriteAllTextAsync(filePath, report.ToString());

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Link Validator Sonuçları",
                    File = new ShareFile(filePath)
                });

                await DisplayAlert("💾 Kaydedildi", $"Sonuçlar başarıyla kaydedildi!\n\n{fileName}", "Tamam");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Hata", $"Kaydetme hatası:\n{ex.Message}", "Tamam");
            }
        }

        // DETAYLI RAPOR
        private async Task GenerateDetailedReport()
        {
            if (testResults.Count == 0)
            {
                await DisplayAlert("ℹ️ Bilgi", "Rapor için test sonucu bulunamadı!", "Tamam");
                return;
            }

            var validForBacklink = testResults.Where(r => r.IsActive && r.HasCommentArea).Count();

            await DisplayAlert("📊 Detaylı Rapor",
                $"🔍 LINK VALIDATOR RAPORU\n\n" +
                $"📊 TEST İSTATİSTİKLERİ:\n" +
                $"Toplam Test: {totalTested}\n" +
                $"✅ Aktif Linkler: {activeLinks}\n" +
                $"💬 Yorum Alanı Var: {commentAreaLinks}\n" +
                $"❌ Ölü Linkler: {deadLinks}\n" +
                $"⚡ Ortalama Hız: {(responseTimes.Count > 0 ? responseTimes.Average() : 0):F0}ms\n" +
                $"🎯 Başarı Oranı: %{GetSuccessRate():F1}\n\n" +
                $"🚀 BACKLINK İÇİN UYGUN: {validForBacklink} link\n\n" +
                $"💡 Bu linkler BacklinkBot'ta kullanılabilir!", "Kapat");
        }

        // HELPER METODLAR
        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        private double GetSuccessRate()
        {
            return totalTested > 0 ? (double)activeLinks / totalTested * 100 : 0;
        }

        private int ParseResponseTime(string responseTime)
        {
            if (string.IsNullOrEmpty(responseTime)) return 0;
            var match = Regex.Match(responseTime, @"(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private void UpdateLabels()
        {
            TestTimeoutLabel.Text = $"{testTimeout} saniye";
        }

        private void StopTesting()
        {
            isTestRunning = false;
            cancellationTokenSource?.Cancel();
            StopTestButton.IsEnabled = false;
            StatusLabel.Text = "⏹️ Test durduruldu";
        }

        private void ClearAllResults()
        {
            testResults.Clear();
            filteredResults.Clear();
            totalTested = 0;
            activeLinks = 0;
            commentAreaLinks = 0;
            deadLinks = 0;
            responseTimes.Clear();
            TestProgressBar.Progress = 0;
            UpdateStats();
            StatusLabel.Text = "💤 Bekleniyor...";
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

        // SAYFA KAPANIRKEN
        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            if (isTestRunning)
            {
                StopTesting();
            }
        }
    }

    // TEST RESULT MODEL
    public class TestResult
    {
        public string Url { get; set; } = "";
        public bool IsActive { get; set; } = false;
        public bool HasCommentArea { get; set; } = false;
        public string Status { get; set; } = "";
        public string StatusIcon { get; set; } = "";
        public string StatusCode { get; set; } = "";
        public string ResponseTime { get; set; } = "";
        public string Details { get; set; } = "";
        public Color StatusColor { get; set; } = Colors.Gray;
    }
}