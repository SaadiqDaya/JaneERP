using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using JaneERP.Security;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class StoreRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        /// <summary>
        /// Normalizes a Shopify store URL or domain to a bare host name.
        /// Accepts full URLs (https://store.myshopify.com) or bare domains.
        /// </summary>
        public static string NormalizeStoreDomain(string storeInput)
        {
            storeInput = storeInput?.Trim() ?? "";
            if (string.IsNullOrEmpty(storeInput))
                throw new ArgumentException("Store domain is required (e.g. vangovapes.myshopify.com)");

            if (storeInput.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                storeInput.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try { return new Uri(storeInput).Host; }
                catch
                {
                    return storeInput
                        .Replace("http://",  "", StringComparison.OrdinalIgnoreCase)
                        .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                        .Trim().TrimEnd('/');
                }
            }

            return storeInput.TrimEnd('/');
        }

        /// <summary>Strips https://, http://, and trailing slashes so we always store a bare hostname.</summary>
        private static string NormalizeDomain(string domain)
        {
            domain = domain.Trim();
            if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                domain = new Uri(domain).Host;
            }
            return domain.TrimEnd('/').ToLowerInvariant();
        }

        private static string TokenKey(string domain) => $"store_{NormalizeDomain(domain)}";

        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Stores' AND xtype='U')
                CREATE TABLE Stores (
                    StoreID     INT           IDENTITY(1,1) PRIMARY KEY,
                    StoreName   NVARCHAR(200) NOT NULL,
                    StoreDomain NVARCHAR(200) NOT NULL,
                    IsActive    BIT           NOT NULL DEFAULT 1,
                    CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT UQ_Stores_StoreDomain UNIQUE (StoreDomain)
                );");
        }

        public IEnumerable<ShopifyStore> GetAll()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var stores = db.Query<ShopifyStore>("SELECT * FROM Stores ORDER BY StoreName").ToList();
            foreach (var s in stores)
                s.Token = SecureStore.GetSecret(TokenKey(s.StoreDomain));
            return stores;
        }

        public ShopifyStore Add(string name, string domain, string token)
        {
            domain = NormalizeDomain(domain);
            using IDbConnection db = new SqlConnection(_connectionString);
            var id = db.QuerySingle<int>(@"
                INSERT INTO Stores (StoreName, StoreDomain)
                VALUES (@name, @domain);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { name, domain });

            SecureStore.SaveSecret(TokenKey(domain), token);

            return new ShopifyStore
            {
                StoreID     = id,
                StoreName   = name,
                StoreDomain = domain,
                IsActive    = true,
                Token       = token
            };
        }

        public void Update(int storeId, string name, string domain, string? newToken)
        {
            domain = NormalizeDomain(domain);
            using IDbConnection db = new SqlConnection(_connectionString);

            // If domain is changing, migrate the token key
            var old = db.QueryFirstOrDefault<ShopifyStore>(
                "SELECT * FROM Stores WHERE StoreID = @storeId", new { storeId });

            db.Execute(@"
                UPDATE Stores SET StoreName = @name, StoreDomain = @domain
                WHERE StoreID = @storeId",
                new { name, domain, storeId });

            if (old != null && old.StoreDomain != domain)
            {
                SecureStore.DeleteSecret(TokenKey(old.StoreDomain));
            }

            if (!string.IsNullOrWhiteSpace(newToken))
                SecureStore.SaveSecret(TokenKey(domain), newToken);
        }

        public void Delete(int storeId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var store = db.QueryFirstOrDefault<ShopifyStore>(
                "SELECT * FROM Stores WHERE StoreID = @storeId", new { storeId });
            if (store == null) return;

            db.Execute("DELETE FROM Stores WHERE StoreID = @storeId", new { storeId });
            SecureStore.DeleteSecret(TokenKey(store.StoreDomain));
        }
    }
}
