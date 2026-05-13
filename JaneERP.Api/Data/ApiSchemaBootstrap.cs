using Dapper;
using JaneERP.Api.Middleware;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

/// <summary>
/// Runs at startup to ensure optional columns exist in every configured company DB.
/// Each migration is executed independently so a single failure doesn't block others.
/// </summary>
public static class ApiSchemaBootstrap
{
    public static void Run(IConfiguration config)
    {
        var companies = config.GetSection("Companies").Get<List<CompanyConfig>>();
        foreach (var company in companies ?? [])
        {
            try
            {
                using var db = new SqlConnection(company.ConnectionString);
                db.Open();

                // Run each migration independently — a failed one won't block the rest
                var migrations = new[]
                {
                    // PurchaseOrders
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PurchaseOrders') AND name='ShippingCost') ALTER TABLE PurchaseOrders ADD ShippingCost DECIMAL(18,2) NOT NULL DEFAULT 0",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PurchaseOrders') AND name='Notes') ALTER TABLE PurchaseOrders ADD Notes NVARCHAR(1000) NULL",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PurchaseOrders') AND name='CreatedBy') ALTER TABLE PurchaseOrders ADD CreatedBy NVARCHAR(100) NULL",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PurchaseOrders') AND name='CreatedAt') ALTER TABLE PurchaseOrders ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE()",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PurchaseOrders') AND name='OverdueNotifiedAt') ALTER TABLE PurchaseOrders ADD OverdueNotifiedAt DATETIME NULL",

                    // SalesOrders
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='ShippingCost') ALTER TABLE SalesOrders ADD ShippingCost DECIMAL(18,2) NOT NULL DEFAULT 0",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='IsPaid') ALTER TABLE SalesOrders ADD IsPaid BIT NOT NULL DEFAULT 0",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PaidAt') ALTER TABLE SalesOrders ADD PaidAt DATETIME NULL",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='InventoryAffected') ALTER TABLE SalesOrders ADD InventoryAffected BIT NOT NULL DEFAULT 0",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='DiscountAmount') ALTER TABLE SalesOrders ADD DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='DiscountPercent') ALTER TABLE SalesOrders ADD DiscountPercent DECIMAL(5,2) NOT NULL DEFAULT 0",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='DiscountType') ALTER TABLE SalesOrders ADD DiscountType NVARCHAR(50) NULL",

                    // Products
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='LastVerifiedAt') ALTER TABLE Products ADD LastVerifiedAt DATETIME NULL",
                    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='LastVerifiedBy') ALTER TABLE Products ADD LastVerifiedBy NVARCHAR(100) NULL",

                    // Stores — LastSyncAt added for mobile sync tracking
                    "IF EXISTS (SELECT 1 FROM sysobjects WHERE name='Stores' AND xtype='U') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Stores') AND name='LastSyncAt') ALTER TABLE Stores ADD LastSyncAt DATETIME NULL",
                };

                foreach (var sql in migrations)
                {
                    try { db.Execute(sql); }
                    catch { /* column already exists, table missing, or permission denied — skip */ }
                }
            }
            catch
            {
                // DB not reachable at startup — will fail at request time with a clearer error
            }
        }
    }
}
