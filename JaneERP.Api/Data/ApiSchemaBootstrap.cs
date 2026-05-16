using Dapper;
using JaneERP.Api.Middleware;
using JaneERP.Core.Services;
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
                    // AuditLog — central audit table for all write operations from the API
                    @"IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='AuditLog' AND xtype='U')
                      CREATE TABLE AuditLog (
                          AuditID   INT IDENTITY(1,1) PRIMARY KEY,
                          UserName  NVARCHAR(100) NOT NULL DEFAULT 'system',
                          Action    NVARCHAR(200) NOT NULL,
                          Details   NVARCHAR(MAX) NULL,
                          LoggedAt  DATETIME      NOT NULL DEFAULT GETDATE()
                      )",

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

                    // CookSessions — batch loss % stored at session creation
                    "IF EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessions' AND xtype='U') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CookSessions') AND name='BatchLossPercent') ALTER TABLE CookSessions ADD BatchLossPercent DECIMAL(5,2) NOT NULL DEFAULT 0",

                    // CookSessionBatches — flask vessel and pre-computed batch size
                    "IF EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessionBatches' AND xtype='U') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CookSessionBatches') AND name='FlaskType') ALTER TABLE CookSessionBatches ADD FlaskType NVARCHAR(50) NULL",
                    "IF EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessionBatches' AND xtype='U') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CookSessionBatches') AND name='BatchSizeML') ALTER TABLE CookSessionBatches ADD BatchSizeML DECIMAL(12,3) NULL",

                    // CookSessionSteps — pre-computed loss-adjusted required quantity
                    "IF EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessionSteps' AND xtype='U') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CookSessionSteps') AND name='RequiredQtyML') ALTER TABLE CookSessionSteps ADD RequiredQtyML DECIMAL(12,3) NULL",

                    // Tasks — shared with desktop app; create if not yet present
                    @"IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Tasks' AND xtype='U')
                      CREATE TABLE Tasks (
                          TaskID      INT            IDENTITY(1,1) PRIMARY KEY,
                          Title       NVARCHAR(200)  NOT NULL,
                          Description NVARCHAR(2000) NULL,
                          AssignedTo  NVARCHAR(100)  NOT NULL,
                          CreatedBy   NVARCHAR(100)  NOT NULL,
                          DueDate     DATETIME       NOT NULL,
                          Status      NVARCHAR(50)   NOT NULL DEFAULT 'Open',
                          Priority    NVARCHAR(50)   NOT NULL DEFAULT 'Normal',
                          CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE()
                      )",

                    @"IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskComments' AND xtype='U')
                      CREATE TABLE TaskComments (
                          CommentID  INT            IDENTITY(1,1) PRIMARY KEY,
                          TaskID     INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                          Username   NVARCHAR(100)  NOT NULL,
                          Body       NVARCHAR(2000) NOT NULL,
                          CreatedAt  DATETIME       NOT NULL DEFAULT GETDATE()
                      )",

                    // Add Priority to existing Tasks tables created before this column existed
                    "IF EXISTS (SELECT 1 FROM sysobjects WHERE name='Tasks' AND xtype='U') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Tasks') AND name='Priority') ALTER TABLE Tasks ADD Priority NVARCHAR(50) NOT NULL DEFAULT 'Normal'",

                    // ── PartLots (Phase 2 lot tracking — FEFO support) ──────────────────
                    @"IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PartLots' AND xtype='U')
                      CREATE TABLE PartLots (
                          LotID          INT IDENTITY(1,1) PRIMARY KEY,
                          PartID         INT           NOT NULL REFERENCES Parts(PartID),
                          LocationID     INT           NULL,
                          LotNumber      NVARCHAR(100) NULL,
                          ExpirationDate DATETIME      NULL,
                          Quantity       INT           NOT NULL DEFAULT 0,
                          ReceivedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
                          Notes          NVARCHAR(500) NULL
                      )",

                    // PartsReservations — link each reservation to a specific lot
                    "IF EXISTS (SELECT 1 FROM sysobjects WHERE name='PartsReservations' AND xtype='U') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PartsReservations') AND name='LotID') ALTER TABLE PartsReservations ADD LotID INT NULL",

                    // WorkOrders — timestamp for when the WO went Live
                    "IF EXISTS (SELECT 1 FROM sysobjects WHERE name='WorkOrders' AND xtype='U') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WorkOrders') AND name='LiveAt') ALTER TABLE WorkOrders ADD LiveAt DATETIME NULL",

                    // WorkOrders — finished-goods output location
                    "IF EXISTS (SELECT 1 FROM sysobjects WHERE name='WorkOrders' AND xtype='U') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WorkOrders') AND name='OutputLocationID') ALTER TABLE WorkOrders ADD OutputLocationID INT NULL",
                };

                foreach (var sql in migrations)
                {
                    try { db.Execute(sql); }
                    catch (Exception migEx)
                    {
                        // Column already exists, table missing, or permission denied — skip.
                        System.Diagnostics.Debug.WriteLine($"[ApiSchemaBootstrap] Migration skipped: {migEx.Message}");
                    }
                }

                // WorkOrderService owns its own DDL (PartLots, LiveAt, OutputLocationID)
                try { WorkOrderService.EnsureSchema(company.ConnectionString); }
                catch (Exception wsEx) { System.Diagnostics.Debug.WriteLine($"[ApiSchemaBootstrap] WorkOrderService schema: {wsEx.Message}"); }
            }
            catch (Exception dbEx)
            {
                // DB not reachable at startup — will fail at request time with a clearer error
                System.Diagnostics.Debug.WriteLine($"[ApiSchemaBootstrap] Company '{company.Name}' DB not reachable at startup: {dbEx.Message}");
            }
        }
    }
}
