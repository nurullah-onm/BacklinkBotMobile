using System.Text.Json;

namespace BacklinkBotMobile
{
    // PROXY SERVER SINIFI (ANA MODEL)
    public class ProxyServer
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 80;
        public string Password { get; set; } = "";
        public string Country { get; set; } = "";
        public string CountryName { get; set; } = "";
        public string AliasName { get; set; } = "";
        public int Load { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime LastTested { get; set; } = DateTime.MinValue;
        public bool IsPremium { get; set; } = false;
        public int Distance { get; set; } = 0;
        public TimeSpan PingTime { get; set; } = TimeSpan.Zero;

        public string DisplayName => $"{AliasName} ({Host})";
        public string FullAddress => $"{Host}:{Port}";
        public string LoadText => $"{Load}% load";

        public override string ToString()
        {
            return $"{Country} {AliasName} ({Host}:{Port}) - Load: {Load}";
        }
    }

    // PROXY RESPONSE SINIFI (JSON İÇİN)
    public class ProxyResponse
    {
        public ServersData ServersData { get; set; }
    }

    public class ServersData
    {
        public string Timestamp { get; set; }
        public ProxyData Data { get; set; }
    }

    public class ProxyData
    {
        public List<RawProxyServer> Servers { get; set; } = new();
    }

    // RAW PROXY SERVER (JSON'DAN GELDİĞİ GİBİ)
    public class RawProxyServer
    {
        public int Load { get; set; }
        public string Country { get; set; } = "";
        public string CountryName { get; set; } = "";
        public string AliasName { get; set; } = "";
        public string Host { get; set; } = "";
        public string Password { get; set; } = "";
        public int Port { get; set; } = 80;
        public bool IsPremium { get; set; } = false;
        public int Distance { get; set; } = 0;
        public List<object> ServiceData { get; set; } = new();

        // Conversion method
        public ProxyServer ToProxyServer()
        {
            return new ProxyServer
            {
                Host = this.Host,
                Port = this.Port == 0 ? 80 : this.Port,
                Password = this.Password,
                Country = this.Country,
                CountryName = this.CountryName,
                AliasName = this.AliasName,
                Load = this.Load,
                IsPremium = this.IsPremium,
                IsActive = true,
                Distance = this.Distance
            };
        }
    }

    // PROXY İSTATİSTİKLERİ (TEK TANIM)
    public class ProxyStats
    {
        public int TotalProxies { get; set; }
        public int ActiveProxies { get; set; }
        public int Countries { get; set; }
        public int AverageLoad { get; set; }
        public int BestLoad { get; set; }
        public int WorstLoad { get; set; }
    }

    // UNIFIED PROXY EXTENSIONS (BÜTÜN EXTENSION'LAR BURADA)
    public static class ProxyExtensions
    {
        public static string GetCountryEmoji(this string countryCode)
        {
            return countryCode switch
            {
                "US" => "🇺🇸",
                "CA" => "🇨🇦",
                "AT" => "🇦🇹",
                "BE" => "🇧🇪",
                "BR" => "🇧🇷",
                "DE" => "🇩🇪",
                "FR" => "🇫🇷",
                "GB" => "🇬🇧",
                "AU" => "🇦🇺",
                "JP" => "🇯🇵",
                "NL" => "🇳🇱",
                "SE" => "🇸🇪",
                "NO" => "🇳🇴",
                "DK" => "🇩🇰",
                "FI" => "🇫🇮",
                "IT" => "🇮🇹",
                "ES" => "🇪🇸",
                "CH" => "🇨🇭",
                "IE" => "🇮🇪",
                "PL" => "🇵🇱",
                "CZ" => "🇨🇿",
                "HU" => "🇭🇺",
                "RO" => "🇷🇴",
                "BG" => "🇧🇬",
                "HR" => "🇭🇷",
                "SI" => "🇸🇮",
                "SK" => "🇸🇰",
                "LT" => "🇱🇹",
                "LV" => "🇱🇻",
                "EE" => "🇪🇪",
                _ => "🌍"
            };
        }

        public static Color GetLoadColor(this int load)
        {
            return load switch
            {
                < 300 => Colors.LimeGreen,      // Mükemmel (yeşil)
                < 500 => Colors.Green,          // Çok iyi (koyu yeşil)
                < 700 => Colors.Orange,         // İyi (turuncu)
                < 900 => Colors.Yellow,         // Orta (sarı)
                < 1200 => Colors.OrangeRed,     // Kötü (turuncu-kırmızı)
                _ => Colors.Red                // Çok kötü (kırmızı)
            };
        }

        public static string GetLoadDescription(this int load)
        {
            return load switch
            {
                < 300 => "Mükemmel ⚡",
                < 500 => "Çok İyi 🟢",
                < 700 => "İyi 🟡",
                < 900 => "Orta 🟠",
                < 1200 => "Yavaş 🔴",
                _ => "Çok Yavaş 🔴"
            };
        }

        public static string GetSpeedEmoji(this int load)
        {
            return load switch
            {
                < 300 => "⚡",
                < 500 => "🚀",
                < 700 => "🟢",
                < 900 => "🟡",
                < 1200 => "🟠",
                _ => "🔴"
            };
        }

        public static bool IsGoodProxy(this int load)
        {
            return load < 700; // 700'den düşük load iyi kabul edilir
        }

        public static string GetQualityRating(this int load)
        {
            return load switch
            {
                < 300 => "⭐⭐⭐⭐⭐",
                < 500 => "⭐⭐⭐⭐",
                < 700 => "⭐⭐⭐",
                < 900 => "⭐⭐",
                _ => "⭐"
            };
        }
    }
}