using System.Text.Json;

namespace JaneERP.Security
{
    /// <summary>
    /// Manages the list of company databases the user can connect to.
    /// Stored in %AppData%\JaneERP\companies.json.
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
                return JsonSerializer.Deserialize<List<CompanyProfile>>(json) ?? DefaultList();
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
            ActiveConnectionString = company.ConnectionString;
            ActiveCompanyName      = company.Name;
        }

        private static List<CompanyProfile> DefaultList() => new()
        {
            new CompanyProfile
            {
                Name             = "JaneERP (Default)",
                ConnectionString = "Server=localhost\\SQLEXPRESS;Database=JaneERP;Integrated Security=True;TrustServerCertificate=True;"
            }
        };
    }

    public class CompanyProfile
    {
        public string Name             { get; set; } = "New Company";
        public string ConnectionString { get; set; } = "";
        public override string ToString() => Name;
    }
}
