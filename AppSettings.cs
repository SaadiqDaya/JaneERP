using System.Text.Json;

namespace JaneERP
{
    /// <summary>Persists user-configurable settings to settings.json in the app directory.</summary>
    public class AppSettings
    {
        private static readonly string _path =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        private static readonly JsonSerializerOptions _jsonOpts =
            new JsonSerializerOptions { WriteIndented = true };

        // ── Settings fields ───────────────────────────────────────────────────────
        public string LogoPath        { get; set; } = Theme.DefaultLogoPath;
        public string JanePhone       { get; set; } = "6042274507";
        public string OpheliaPhone    { get; set; } = "18665782646";
        public string HomeCurrency    { get; set; } = "CAD";
        public string DefaultCurrency { get; set; } = "CAD";

        /// <summary>
        /// Exchange rates relative to the home currency (HomeCurrency).
        /// Key = currency code (e.g. "USD"), Value = units of HomeCurrency per 1 unit of this currency.
        /// Example: USD → 1.45 means 1 USD = 1.45 CAD.
        /// </summary>
        public Dictionary<string, decimal> CurrencyRates { get; set; } = new()
        {
            { "USD", 1.45m }
        };

        /// <summary>
        /// Custom order types available when creating a manual order.
        /// "Shopify" is a reserved system type and is not included here.
        /// Defaults: Manual, Phone, Walk-in, POS.
        /// </summary>
        public List<string> OrderTypes { get; set; } = new()
        {
            "Manual", "Phone", "Walk-in", "POS"
        };

        // ── Email / SMTP ──────────────────────────────────────────────────────────────
        public string SmtpServer   { get; set; } = "";
        public int    SmtpPort     { get; set; } = 587;
        public bool   SmtpUseSsl   { get; set; } = true;
        public string SmtpUser     { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public string FromEmail    { get; set; } = "";
        public string FromName     { get; set; } = "JaneERP";

        /// <summary>Attribute names pinned as filter buttons on the Product Search screen.</summary>
        public List<string> ProductSearchPinnedAttributes { get; set; } = new();

        /// <summary>True if SMTP is configured enough to send email.</summary>
        public bool IsEmailConfigured =>
            !string.IsNullOrWhiteSpace(SmtpServer) &&
            !string.IsNullOrWhiteSpace(FromEmail)  &&
            !string.IsNullOrWhiteSpace(SmtpUser);

        /// <summary>Returns the rate for the given currency code against the home currency.
        /// Returns 1 if the code is the home currency or unknown.</summary>
        public decimal GetRate(string currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode) ||
                currencyCode.Equals(HomeCurrency, StringComparison.OrdinalIgnoreCase))
                return 1m;
            return CurrencyRates.TryGetValue(currencyCode.ToUpper(), out var r) ? r : 1m;
        }

        // ── Singleton ─────────────────────────────────────────────────────────────
        private static AppSettings? _current;
        public static AppSettings Current
        {
            get
            {
                _current ??= Load();
                return _current;
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    return _current;
                }
            }
            catch (Exception ex) { Logging.AppLogger.Info($"[AppSettings.Load]: {ex.Message}"); }

            _current = new AppSettings();
            return _current;
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(this, _jsonOpts));
                _current = this;
            }
            catch (Exception ex) { Logging.AppLogger.Info($"[AppSettings.Save]: {ex.Message}"); }
        }

        /// <summary>Returns the logo Image, falling back to the bundled default.</summary>
        public Image? LoadLogoImage()
        {
            try
            {
                if (File.Exists(LogoPath))         return Image.FromFile(LogoPath);
                if (File.Exists(Theme.DefaultLogoPath)) return Image.FromFile(Theme.DefaultLogoPath);
            }
            catch (Exception ex) { Logging.AppLogger.Info($"[AppSettings.LoadLogoImage]: {ex.Message}"); }
            return null;
        }
    }
}
