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
            catch (Exception ex) { Logging.AppLogger.Error($"[AppSettings.Protect] {ex}"); return ""; }
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
            catch (Exception ex) { Logging.AppLogger.Error($"[AppSettings.Unprotect] {ex}"); return ""; }
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

        /// <summary>
        /// Available shipping methods shown in the fulfilment workflow (picking dash, quick fulfil).
        /// Defaults: Standard, Express, Overnight, Local Pickup.
        /// </summary>
        public List<string> ShippingMethods { get; set; } = new()
        {
            "Standard", "Express", "Overnight", "Local Pickup"
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
        // NOTE: AppSettings does not currently track LastModifiedBy / LastModifiedAt.
        // The settings.json file's filesystem timestamp serves as a rough "last saved" indicator.
        // If per-user change tracking is needed in the future, add:
        //   public string?   LastModifiedBy { get; set; }
        //   public DateTime? LastModifiedAt { get; set; }
        // and populate them in Save() via AppSession.CurrentUser?.Username.

        // ── Manufacturing ─────────────────────────────────────────────────────────

        /// <summary>Default hourly labour rate used when adding a new labour row to a BOM. 0 = no default.</summary>
        public decimal DefaultLabourRate { get; set; } = 0m;

        /// <summary>
        /// Flask (mixing vessel) thresholds used to assign the correct container to a batch.
        /// Sorted ascending by MaxBatchMl — first entry where BatchSizeML ≤ MaxBatchMl is used.
        /// </summary>
        public List<FlaskConfig> FlaskConfigs { get; set; } = new()
        {
            new FlaskConfig { Name = "1L Squeeze",    MaxBatchMl = 1_000   },
            new FlaskConfig { Name = "10L Jug",       MaxBatchMl = 9_000   },
            new FlaskConfig { Name = "20L Stainless", MaxBatchMl = 18_000  },
            new FlaskConfig { Name = "100L Vat",      MaxBatchMl = 999_999 },
        };

        /// <summary>
        /// Quick-select batch-loss presets shown in the cook session launcher.
        /// Workers pick the appropriate preset for the bottle size they are making.
        /// </summary>
        public List<BatchLossPreset> BatchLossPresets { get; set; } = new()
        {
            new BatchLossPreset { Label = "10ml / 30ml bottles", Percent = 15m },
            new BatchLossPreset { Label = "30ml Glass",          Percent = 7m  },
            new BatchLossPreset { Label = "60ml bottles",        Percent = 6m  },
            new BatchLossPreset { Label = "120ml+",              Percent = 3m  },
        };

        /// <summary>
        /// Returns the flask name for the given batch size in ml.
        /// Walks FlaskConfigs in ascending MaxBatchMl order and returns the first match.
        /// Returns the last config name if no threshold is matched (i.e. largest vessel).
        /// </summary>
        public string GetFlaskForBatchMl(decimal batchMl)
        {
            var sorted = FlaskConfigs.OrderBy(f => f.MaxBatchMl).ToList();
            foreach (var fc in sorted)
                if (batchMl <= fc.MaxBatchMl) return fc.Name;
            return sorted.LastOrDefault()?.Name ?? "Unknown";
        }

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

        /// <summary>Returns the logo image, falling back to the bundled default.</summary>
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

    /// <summary>Maps a batch-size threshold (ml) to the flask/vessel name used at that size.</summary>
    public class FlaskConfig
    {
        public string  Name       { get; set; } = "";
        /// <summary>Maximum batch size in ml for this flask. Batches ≤ this value use this flask.</summary>
        public decimal MaxBatchMl { get; set; }
    }

    /// <summary>A named batch-loss percentage preset for the cook session launcher.</summary>
    public class BatchLossPreset
    {
        public string  Label   { get; set; } = "";
        public decimal Percent { get; set; }
    }
}
