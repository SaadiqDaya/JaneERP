using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JaneERP
{
    /// <summary>Persists user-configurable settings to settings.json in the app directory.</summary>
    public class AppSettings
    {
        // ── DPAPI helpers (Windows Data Protection API) ───────────────────────────
        /// <summary>Encrypts a plain-text string using the current Windows user account key.
        /// Returns a Base64-encoded ciphertext. Returns empty string for null/empty input.</summary>
        private static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            try
            {
                var bytes      = Encoding.UTF8.GetBytes(plainText);
                var encrypted  = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch { return ""; }
        }

        /// <summary>Decrypts a DPAPI-protected Base64 string back to plain text.
        /// Returns empty string on failure (e.g. wrong user account or corrupt data).</summary>
        private static string Unprotect(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            try
            {
                var bytes     = Convert.FromBase64String(cipherText);
                var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch { return ""; }
        }

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

        // ── Security / Lockout policy ─────────────────────────────────────────────────
        /// <summary>Failed attempts before account lockout. 0 = use built-in default (5).</summary>
        public int    MaxLoginAttempts { get; set; } = 5;
        /// <summary>How many minutes an account stays locked. 0 = use built-in default (15).</summary>
        public int    LockoutMinutes   { get; set; } = 15;
        /// <summary>Admin phone number shown in lockout messages.</summary>
        public string AdminPhone       { get; set; } = "";
        /// <summary>Admin email shown in lockout messages.</summary>
        public string AdminEmail       { get; set; } = "";
        /// <summary>When true, pre-fills the username field with the last successful login name.</summary>
        public bool   RememberLastUsername { get; set; } = false;
        /// <summary>Stores the most recently used username (only used when RememberLastUsername is true).</summary>
        public string LastUsername     { get; set; } = "";

        // ── Email / SMTP ──────────────────────────────────────────────────────────────
        public string SmtpServer   { get; set; } = "";
        public int    SmtpPort     { get; set; } = 587;
        public bool   SmtpUseSsl   { get; set; } = true;
        public string SmtpUser     { get; set; } = "";

        /// <summary>DPAPI-encrypted SMTP password stored in settings.json.
        /// Use SmtpPasswordPlain to read/write the decrypted value at runtime.</summary>
        public string SmtpPassword { get; set; } = "";

        /// <summary>Returns the decrypted SMTP password for use at runtime (never persisted).</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string SmtpPasswordPlain
        {
            get => Unprotect(SmtpPassword);
            set => SmtpPassword = Protect(value);
        }

        public string FromEmail    { get; set; } = "";
        public string FromName     { get; set; } = "JaneERP";

        /// <summary>Attribute names pinned as filter buttons on the Product Search screen.</summary>
        public List<string> ProductSearchPinnedAttributes { get; set; } = new();

        // ── Appearance ────────────────────────────────────────────────────────────
        /// <summary>Hex colour string for the primary accent (Theme.Gold). Empty = use default violet.</summary>
        public string AccentColor    { get; set; } = "";
        /// <summary>Hex colour string for the secondary highlight (Theme.Teal). Empty = use default teal.</summary>
        public string HighlightColor { get; set; } = "";

        // ── Export settings ───────────────────────────────────────────────────────
        /// <summary>Default folder for CSV exports. Empty string = ask every time.</summary>
        public string DefaultExportPath { get; set; } = "";

        // ── Backup settings ───────────────────────────────────────────────────────
        /// <summary>Folder where database backups are written.</summary>
        public string BackupFolder    { get; set; } = "";
        /// <summary>Auto-backup schedule: "None", "Daily", or "Weekly".</summary>
        public string BackupSchedule  { get; set; } = "None";
        /// <summary>UTC datetime of the last successful backup.</summary>
        public DateTime? LastBackupAt { get; set; }

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
