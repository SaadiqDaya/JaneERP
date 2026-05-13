using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JaneERP.Security
{
    /// <summary>
    /// Manages the list of company databases the user can connect to.
    /// Stored in %AppData%\JaneERP\companies.json.
    /// Connection strings are DPAPI-encrypted at rest using the Windows current-user key.
    /// </summary>
    public static class CompanyManager
    {
        private static string ConfigPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JaneERP", "companies.json");

        public static List<CompanyProfile> Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return DefaultList();
                var json = File.ReadAllText(ConfigPath);
                var list = JsonSerializer.Deserialize<List<CompanyProfile>>(json) ?? DefaultList();

                // One-time migration: encrypt any plaintext connection strings still in the file
                bool migrated = false;
                foreach (var c in list)
                {
                    if (!string.IsNullOrEmpty(c.ConnectionString) && string.IsNullOrEmpty(c.ConnectionStringEncrypted))
                    {
                        c.ConnectionStringPlain = c.ConnectionString;  // encrypts and clears plaintext
                        migrated = true;
                    }
                }
                if (migrated) Save(list);

                return list;
            }
            catch { return DefaultList(); }
        }

        public static void Save(List<CompanyProfile> companies)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(companies, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static void AddCompany(CompanyProfile company)
        {
            var list = Load();
            list.Add(company);
            Save(list);
        }

        // Runtime active connection — set after company selection
        public static string? ActiveConnectionString { get; private set; }
        public static string? ActiveCompanyName      { get; private set; }

        public static void SetActive(CompanyProfile company)
        {
            ActiveConnectionString = company.ConnectionStringPlain;
            ActiveCompanyName      = company.Name;
        }

        private static List<CompanyProfile> DefaultList() => new()
        {
            new CompanyProfile
            {
                Name                  = "JaneERP (Default)",
                ConnectionStringPlain = "Server=localhost\\SQLEXPRESS;Database=JaneERP;Integrated Security=True;TrustServerCertificate=True;"
            }
        };
    }

    public class CompanyProfile
    {
        // ── DPAPI helpers (same pattern as AppSettings.SmtpPassword) ─────────────
        private static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            try
            {
                var bytes     = Encoding.UTF8.GetBytes(plainText);
                var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch { return ""; }
        }

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

        public string Name { get; set; } = "New Company";

        /// <summary>DPAPI-encrypted connection string stored in companies.json.
        /// Use <see cref="ConnectionStringPlain"/> at runtime.</summary>
        public string ConnectionStringEncrypted { get; set; } = "";

        /// <summary>Legacy plaintext field retained for one-time migration from pre-encryption installs.
        /// Cleared automatically on first load after upgrade.</summary>
        public string ConnectionString { get; set; } = "";

        /// <summary>Decrypts and returns the connection string for runtime use. Never written to JSON.</summary>
        [JsonIgnore]
        public string ConnectionStringPlain
        {
            get
            {
                // Prefer the encrypted form; fall back to legacy plaintext during migration window
                if (!string.IsNullOrEmpty(ConnectionStringEncrypted))
                    return Unprotect(ConnectionStringEncrypted);
                return ConnectionString;
            }
            set
            {
                ConnectionStringEncrypted = Protect(value);
                ConnectionString = "";   // clear plaintext immediately
            }
        }

        public override string ToString() => Name;
    }
}
