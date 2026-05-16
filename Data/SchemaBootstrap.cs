using System.Configuration;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    /// <summary>
    /// Single authoritative source for all SQL Server schema creation and evolution.
    /// Run against any empty or existing company database to bring it fully up to date.
    /// Every statement uses IF NOT EXISTS / IF EXISTS guards — safe to run repeatedly.
    /// </summary>
    public static class SchemaBootstrap
    {
        /// <summary>
        /// Applies the complete schema to the given connection string.
        /// Called from Program.cs after a company database is selected or created.
        /// <paramref name="progress"/> is optional — receives (stepName, currentStep, totalSteps).
        /// </summary>
        public static void Run(string connectionString, Action<string, int, int>? progress = null)
        {
            int cur = 0;
            const int total = 76;

            void S(string name, string sql)
            {
                progress?.Invoke(name, ++cur, total);
                Step(connectionString, name, sql);
            }

            // ── 1. Foundation tables (no FK dependencies) ──────────────────────
            S("Users",              Sql.Users);
            S("UserColumns",        Sql.UserColumns);
            S("UserLockout",        Sql.UserLockout);
            S("Stores",             Sql.Stores);
            S("Vendors",            Sql.Vendors);
            S("ProductTypes",       Sql.ProductTypes);
            S("ProductTypeAttrs",   Sql.ProductTypeAttrs);
            S("AttributeDefs",      Sql.AttributeDefinitions);
            S("UOMs",               Sql.UnitOfMeasures);
            S("UOMConversions",     Sql.UomConversions);

            // ── 2. Products (FK to Vendors, ProductTypes) ─────────────────────
            S("Products",           Sql.Products);
            S("InvTransactions",    Sql.InventoryTransactions);
            S("ProductAttributes",  Sql.ProductAttributes);
            S("ProductTypeSeeds",   Sql.ProductTypeSeeds);

            // ── 3. Locations (adds FK columns to Products + InventoryTransactions) ─
            S("Locations",          Sql.Locations);
            S("LocationFKColumns",  Sql.LocationFKColumns);
            S("SeedLocations",      Sql.SeedLocations);
            S("LocationBins",       Sql.LocationBins);
            S("LocationCycleSchedule", Sql.LocationCycleSchedule);

            // ── 4. Parts (FK to Vendors, Products) ────────────────────────────
            S("Parts",              Sql.Parts);
            S("PartColumns",        Sql.PartColumns);
            S("ProductParts",       Sql.ProductParts);
            S("ProductPartsDecimal",Sql.ProductPartsDecimalMigration);
            S("BomLabourCosts",     Sql.BomLabourCosts);

            // ── 5. ProductColumns (FK to Vendors, ProductTypes — must be after those tables) ──
            S("ProductColumns",     Sql.ProductColumns);

            // ── 6. Customers, Sales (FK to Products, Stores, Customers, Locations) ──
            S("Customers",          Sql.Customers);
            S("SalesOrders",        Sql.SalesOrders);
            S("SalesOrderItems",    Sql.SalesOrderItems);
            S("SalesOrderColumns",  Sql.SalesOrderColumns);
            S("StockReservations",  Sql.StockReservations);
            S("CustomerPayments",   Sql.CustomerPayments);
            S("CustomerNotes",      Sql.CustomerNotes);

            // ── 7. Suppliers & Purchase Orders (FK to Parts, Products) ────────
            S("Suppliers",          Sql.Suppliers);
            S("PurchaseOrders",     Sql.PurchaseOrders);
            S("PurchaseOrderItems", Sql.PurchaseOrderItems);
            S("PurchaseOrderCols",  Sql.PurchaseOrderColumns);

            // ── 8. Manufacturing (FK to Products, Parts) ─────────────────────
            S("ManufacturingOrders",Sql.ManufacturingOrders);
            S("WorkOrders",         Sql.WorkOrders);
            S("WorkOrderColumns",   Sql.WorkOrderColumns);
            S("PartsReservations",  Sql.PartsReservations);
            S("CookSessions",       Sql.CookSessions);
            S("CookSessionBatches", Sql.CookSessionBatches);
            S("CookSessionSteps",   Sql.CookSessionSteps);
            S("CookColumns",        Sql.CookColumns);

            // ── 9. Tasks ──────────────────────────────────────────────────────
            S("TaskWorkflows",      Sql.TaskWorkflows);
            S("TaskWorkflowStatuses", Sql.TaskWorkflowStatuses);
            S("Tasks",              Sql.Tasks);
            S("TaskColumns",        Sql.TaskColumns);
            S("TaskComments",       Sql.TaskComments);
            S("TaskMentions",       Sql.TaskMentions);
            S("TaskLinkedRecords",  Sql.TaskLinkedRecords);
            S("TaskHistory",        Sql.TaskHistory);
            S("TaskSubtasks",       Sql.TaskSubtasks);
            S("TaskTemplates",      Sql.TaskTemplates);
            S("TaskTemplateItems",  Sql.TaskTemplateItems);

            // ── 10. Accounting ────────────────────────────────────────────────
            S("ExpenseCategories",  Sql.ExpenseCategories);
            S("Expenses",           Sql.Expenses);
            S("TaxRates",           Sql.TaxRates);
            S("SeedExpenses",       Sql.SeedExpenseCategories);

            // ── 11. Packages, Discounts, Returns, Backorders ──────────────────
            S("PackageComponents",  Sql.PackageComponents);
            S("DiscountTiers",      Sql.DiscountTiers);
            S("DiscountTierColumns",Sql.DiscountTierColumns);
            S("ReturnOrders",       Sql.ReturnOrders);
            S("ReturnOrderItems",   Sql.ReturnOrderItems);
            S("CustomerCredits",    Sql.CustomerCredits);
            S("ReturnColumns",      Sql.ReturnColumns);
            S("Backorders",         Sql.Backorders);

            // ── 12. Cycle Count (FK to Locations) ─────────────────────────────
            S("CycleCountColumns",  Sql.CycleCountColumns);

            // ── 13. Shipments / Packing ───────────────────────────────────────
            S("BoxTypes",           Sql.BoxTypes);
            S("Shipments",          Sql.Shipments);
            S("ShipmentItems",      Sql.ShipmentItems);
            S("ShipmentColumns",    Sql.ShipmentColumns);

            // ── 14. Performance: migration table, indexes, RCSI, row version ──
            S("AppliedMigrations",  Sql.AppliedMigrations);
            S("RowVersion",         Sql.RowVersion);
            S("EnableRCSI",         Sql.EnableRCSI);
            S("PerformanceIndexes", Sql.PerformanceIndexes);
        }

        private static void Step(string cs, string name, string sql)
        {
            try
            {
                using IDbConnection db = new SqlConnection(cs);
                db.Execute(sql);
            }
            catch (Exception ex)
            {
                Logging.AppLogger.Audit("system", $"SchemaBootstrap_{name}", ex.ToString());
                MessageBox.Show(
                    $"Database setup step '{name}' failed:\n\n{ex.Message}\n\nSome features may not work. Check the log for details.",
                    "Database Setup Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // All SQL statements — idempotent, IF NOT EXISTS guarded
        // ─────────────────────────────────────────────────────────────────────
        private static class Sql
        {
            // ── Users ─────────────────────────────────────────────────────────
            public const string Users = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Users' AND xtype='U')
                CREATE TABLE Users (
                    UserId       INT IDENTITY(1,1) PRIMARY KEY,
                    Username     NVARCHAR(100) NOT NULL UNIQUE,
                    PasswordHash NVARCHAR(256) NOT NULL,
                    PasswordSalt NVARCHAR(256) NOT NULL,
                    Role         NVARCHAR(50)  NOT NULL DEFAULT 'User',
                    IsActive     BIT           NOT NULL DEFAULT 1,
                    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE(),
                    LastLoginAt  DATETIME      NULL
                )";

            public const string UserColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Email')
                    ALTER TABLE Users ADD Email NVARCHAR(200) NOT NULL DEFAULT '';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Permissions')
                    ALTER TABLE Users ADD Permissions NVARCHAR(500) NOT NULL DEFAULT '';";

            public const string UserLockout = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'FailedLoginCount')
                    ALTER TABLE Users ADD FailedLoginCount INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LockedUntil')
                    ALTER TABLE Users ADD LockedUntil DATETIME NULL;";

            // ── Stores ────────────────────────────────────────────────────────
            public const string Stores = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Stores' AND xtype='U')
                CREATE TABLE Stores (
                    StoreID     INT           IDENTITY(1,1) PRIMARY KEY,
                    StoreName   NVARCHAR(200) NOT NULL,
                    StoreDomain NVARCHAR(200) NOT NULL,
                    IsActive    BIT           NOT NULL DEFAULT 1,
                    CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT UQ_Stores_StoreDomain UNIQUE (StoreDomain)
                );
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Stores') AND name='Token')
                    ALTER TABLE Stores ADD Token NVARCHAR(500) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Stores') AND name='LastSyncAt')
                    ALTER TABLE Stores ADD LastSyncAt DATETIME NULL;";

            // ── Vendors ───────────────────────────────────────────────────────
            public const string Vendors = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Vendors')
                    CREATE TABLE Vendors (
                        VendorID    INT IDENTITY PRIMARY KEY,
                        VendorName  NVARCHAR(200) NOT NULL,
                        ContactName NVARCHAR(200) NULL,
                        Email       NVARCHAR(200) NULL,
                        Phone       NVARCHAR(50)  NULL,
                        Website     NVARCHAR(300) NULL,
                        IsActive    BIT NOT NULL DEFAULT 1
                    );";

            // ── Product Types & Attributes ────────────────────────────────────
            public const string ProductTypes = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ProductTypes' AND xtype='U')
                CREATE TABLE ProductTypes (
                    ProductTypeID INT IDENTITY(1,1) PRIMARY KEY,
                    TypeName      NVARCHAR(100) NOT NULL UNIQUE
                );";

            public const string ProductTypeAttrs = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ProductTypeAttributes' AND xtype='U')
                CREATE TABLE ProductTypeAttributes (
                    ProductTypeID  INT           NOT NULL REFERENCES ProductTypes(ProductTypeID) ON DELETE CASCADE,
                    AttributeName  NVARCHAR(100) NOT NULL,
                    IsRequired     BIT           NOT NULL DEFAULT 1,
                    PRIMARY KEY (ProductTypeID, AttributeName)
                );
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductTypeAttributes') AND name = 'IsRequired')
                    ALTER TABLE ProductTypeAttributes ADD IsRequired BIT NOT NULL DEFAULT 1;";

            public const string ProductTypeSeeds = @"
                IF NOT EXISTS (SELECT 1 FROM ProductTypes WHERE TypeName = 'Package')
                    INSERT INTO ProductTypes (TypeName) VALUES ('Package');
                IF NOT EXISTS (SELECT 1 FROM ProductTypes WHERE TypeName = 'Standard')
                    INSERT INTO ProductTypes (TypeName) VALUES ('Standard');";

            public const string AttributeDefinitions = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='AttributeDefinitions' AND xtype='U')
                CREATE TABLE AttributeDefinitions (
                    AttributeDefID INT IDENTITY(1,1) PRIMARY KEY,
                    AttributeName  NVARCHAR(100) NOT NULL UNIQUE,
                    AllowedValues  NVARCHAR(MAX) NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttributeDefinitions') AND name = 'Category')
                    ALTER TABLE AttributeDefinitions ADD Category NVARCHAR(20) NOT NULL DEFAULT 'General';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttributeDefinitions') AND name = 'DataType')
                    ALTER TABLE AttributeDefinitions ADD DataType NVARCHAR(20) NOT NULL DEFAULT 'Text';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttributeDefinitions') AND name = 'Unit')
                    ALTER TABLE AttributeDefinitions ADD Unit NVARCHAR(20) NULL;";

            // ── UOM ───────────────────────────────────────────────────────────
            public const string UnitOfMeasures = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='UnitOfMeasures' AND xtype='U')
                BEGIN
                    CREATE TABLE UnitOfMeasures (
                        UOMID            INT IDENTITY(1,1) PRIMARY KEY,
                        Name             NVARCHAR(50)   NOT NULL,
                        Abbreviation     NVARCHAR(20)   NOT NULL,
                        BaseUnit         NVARCHAR(20)   NULL,
                        ConversionFactor DECIMAL(18,6)  NOT NULL DEFAULT 1,
                        DisplayOrder     INT            NOT NULL DEFAULT 0,
                        IsActive         BIT            NOT NULL DEFAULT 1,
                        CONSTRAINT UQ_UOM_Abbreviation UNIQUE (Abbreviation)
                    );
                    INSERT INTO UnitOfMeasures (Name, Abbreviation, BaseUnit, ConversionFactor, DisplayOrder) VALUES
                        ('Each',        'ea',    'ea',  1,        0),
                        ('Pieces',      'pcs',   'ea',  1,        1),
                        ('Gram',        'g',     'g',   1,        10),
                        ('Kilogram',    'kg',    'g',   1000,     11),
                        ('Milligram',   'mg',    'g',   0.001,    12),
                        ('Millilitre',  'mL',    'mL',  1,        20),
                        ('Litre',       'L',     'mL',  1000,     21),
                        ('Ounce',       'oz',    'g',   28.3495,  30),
                        ('Pound',       'lb',    'g',   453.592,  31),
                        ('Fluid Ounce', 'fl oz', 'mL',  29.5735,  32);
                END";

            public const string UomConversions = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='UomConversions' AND xtype='U')
                BEGIN
                    CREATE TABLE UomConversions (
                        ConversionID INT           IDENTITY(1,1) PRIMARY KEY,
                        FromUOMID    INT           NOT NULL REFERENCES UnitOfMeasures(UOMID) ON DELETE NO ACTION,
                        ToUOMID      INT           NOT NULL REFERENCES UnitOfMeasures(UOMID) ON DELETE NO ACTION,
                        Multiplier   DECIMAL(18,6) NOT NULL DEFAULT 1,
                        CONSTRAINT UQ_UomConversions_Pair UNIQUE (FromUOMID, ToUOMID)
                    );
                    INSERT INTO UomConversions (FromUOMID, ToUOMID, Multiplier)
                    SELECT f.UOMID, t.UOMID,
                           CASE WHEN f.BaseUnit = t.Abbreviation THEN f.ConversionFactor
                                WHEN t.BaseUnit = f.Abbreviation THEN 1.0 / t.ConversionFactor
                                ELSE f.ConversionFactor / t.ConversionFactor END
                    FROM UnitOfMeasures f
                    JOIN UnitOfMeasures t ON f.UOMID <> t.UOMID
                                        AND ISNULL(f.BaseUnit, f.Abbreviation) = ISNULL(t.BaseUnit, t.Abbreviation)
                    WHERE f.IsActive = 1 AND t.IsActive = 1;
                END";

            // ── Products ──────────────────────────────────────────────────────
            public const string Products = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Products' AND xtype='U')
                CREATE TABLE Products (
                    ProductID      INT IDENTITY(1,1) PRIMARY KEY,
                    SKU            NVARCHAR(100)  NOT NULL UNIQUE,
                    ProductName    NVARCHAR(200)  NOT NULL,
                    RetailPrice    DECIMAL(18,2)  NOT NULL DEFAULT 0,
                    WholesalePrice DECIMAL(18,2)  NOT NULL DEFAULT 0,
                    IsActive       BIT            NOT NULL DEFAULT 1,
                    ReorderPoint   INT            NOT NULL DEFAULT 0,
                    OrderUpTo      INT            NOT NULL DEFAULT 0,
                    IsAutoCreated  BIT            NOT NULL DEFAULT 0,
                    IsVerified     BIT            NOT NULL DEFAULT 0
                );";

            public const string InventoryTransactions = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='InventoryTransactions' AND xtype='U')
                CREATE TABLE InventoryTransactions (
                    TransactionID   INT IDENTITY(1,1) PRIMARY KEY,
                    ProductID       INT           NOT NULL REFERENCES Products(ProductID),
                    QuantityChange  INT           NOT NULL,
                    TransactionType NVARCHAR(50)  NOT NULL,
                    Notes           NVARCHAR(500) NULL,
                    TransactionDate DATETIME      NOT NULL DEFAULT GETDATE()
                );";

            public const string ProductAttributes = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ProductAttributes' AND xtype='U')
                CREATE TABLE ProductAttributes (
                    AttributeID    INT IDENTITY(1,1) PRIMARY KEY,
                    ProductID      INT            NOT NULL REFERENCES Products(ProductID),
                    AttributeName  NVARCHAR(100)  NOT NULL,
                    AttributeValue NVARCHAR(500)  NULL
                );";

            // After Vendors + ProductTypes exist, add FK columns to Products
            public const string ProductColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'WholesalePrice')
                    ALTER TABLE Products ADD WholesalePrice DECIMAL(18,2) NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ReorderPoint')
                    ALTER TABLE Products ADD ReorderPoint INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'OrderUpTo')
                    ALTER TABLE Products ADD OrderUpTo INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='DefaultVendorID')
                    ALTER TABLE Products ADD DefaultVendorID INT NULL REFERENCES Vendors(VendorID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='IsAutoCreated')
                    ALTER TABLE Products ADD IsAutoCreated BIT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='IsVerified')
                    ALTER TABLE Products ADD IsVerified BIT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='BomSourceID')
                    ALTER TABLE Products ADD BomSourceID INT NULL REFERENCES Products(ProductID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='BomNumber')
                    ALTER TABLE Products ADD BomNumber NVARCHAR(20) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='UnitOfMeasure')
                    ALTER TABLE Products ADD UnitOfMeasure NVARCHAR(20) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='ProductTypeID')
                    ALTER TABLE Products ADD ProductTypeID INT NULL REFERENCES ProductTypes(ProductTypeID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'DefaultLocationID')
                    ALTER TABLE Products ADD DefaultLocationID INT NULL REFERENCES Locations(LocationID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'LastVerifiedAt')
                    ALTER TABLE Products ADD LastVerifiedAt DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'LastVerifiedBy')
                    ALTER TABLE Products ADD LastVerifiedBy NVARCHAR(100) NULL;";

            // ── Locations ─────────────────────────────────────────────────────
            public const string Locations = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Locations' AND xtype='U')
                CREATE TABLE Locations (
                    LocationID   INT IDENTITY(1,1) PRIMARY KEY,
                    LocationName NVARCHAR(100) NOT NULL,
                    IsActive     BIT           NOT NULL DEFAULT 1
                );
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Locations') AND name = 'Notes')
                    ALTER TABLE Locations ADD Notes NVARCHAR(500) NULL;";

            public const string LocationFKColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryTransactions') AND name = 'LocationID')
                    ALTER TABLE InventoryTransactions ADD LocationID INT NULL REFERENCES Locations(LocationID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryTransactions') AND name = 'LotNumber')
                    ALTER TABLE InventoryTransactions ADD LotNumber NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryTransactions') AND name = 'ExpirationDate')
                    ALTER TABLE InventoryTransactions ADD ExpirationDate DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryTransactions') AND name = 'StoreID')
                    ALTER TABLE InventoryTransactions ADD StoreID INT NULL REFERENCES Stores(StoreID);";

            public const string SeedLocations = @"
                IF NOT EXISTS (SELECT 1 FROM Locations WHERE LocationName = 'Main Warehouse')
                    INSERT INTO Locations (LocationName, IsActive) VALUES ('Main Warehouse', 1);
                IF NOT EXISTS (SELECT 1 FROM Locations WHERE LocationName = 'Shipping Dock')
                    INSERT INTO Locations (LocationName, IsActive) VALUES ('Shipping Dock', 1);
                IF NOT EXISTS (SELECT 1 FROM Locations WHERE LocationName = 'Quality Lab')
                    INSERT INTO Locations (LocationName, IsActive) VALUES ('Quality Lab', 1);";

            public const string LocationBins = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='LocationBins' AND xtype='U')
                CREATE TABLE LocationBins (
                    BinID       INT IDENTITY(1,1) PRIMARY KEY,
                    LocationID  INT NOT NULL REFERENCES Locations(LocationID),
                    BinCode     NVARCHAR(50)  NOT NULL,
                    Description NVARCHAR(200) NULL,
                    Capacity    INT           NULL,
                    IsActive    BIT           NOT NULL DEFAULT 1,
                    CONSTRAINT UQ_LocationBins_Code UNIQUE (LocationID, BinCode)
                );
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('LocationBins') AND name='ShelfSpots')
                    ALTER TABLE LocationBins ADD ShelfSpots INT NULL;";

            public const string LocationCycleSchedule = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='LocationCycleSchedule' AND xtype='U')
                CREATE TABLE LocationCycleSchedule (
                    LocationID    INT  NOT NULL PRIMARY KEY REFERENCES Locations(LocationID) ON DELETE CASCADE,
                    FrequencyDays INT  NULL,
                    LastCountedAt DATETIME NULL
                );";

            // ── Parts ─────────────────────────────────────────────────────────
            public const string Parts = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Parts' AND xtype='U')
                CREATE TABLE Parts (
                    PartID           INT IDENTITY(1,1) PRIMARY KEY,
                    PartNumber       NVARCHAR(100) NOT NULL UNIQUE,
                    PartName         NVARCHAR(200) NOT NULL,
                    Description      NVARCHAR(500) NULL,
                    UnitCost         DECIMAL(18,2) NOT NULL DEFAULT 0,
                    CurrentStock     INT           NOT NULL DEFAULT 0,
                    IsActive         BIT           NOT NULL DEFAULT 1,
                    DefaultVendorID  INT           NULL REFERENCES Vendors(VendorID)
                );
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Parts_CurrentStock_NonNegative')
                    ALTER TABLE Parts ADD CONSTRAINT CK_Parts_CurrentStock_NonNegative CHECK (CurrentStock >= 0);";

            public const string PartColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Parts') AND name='DefaultVendorID')
                    ALTER TABLE Parts ADD DefaultVendorID INT NULL REFERENCES Vendors(VendorID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Parts') AND name='IsAutoCreated')
                    ALTER TABLE Parts ADD IsAutoCreated BIT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Parts') AND name='IsVerified')
                    ALTER TABLE Parts ADD IsVerified BIT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Parts') AND name='UnitOfMeasure')
                    ALTER TABLE Parts ADD UnitOfMeasure NVARCHAR(20) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Parts') AND name='ReorderPoint')
                    ALTER TABLE Parts ADD ReorderPoint INT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Parts') AND name='Density')
                    ALTER TABLE Parts ADD Density DECIMAL(6,4) NULL;";

            public const string ProductParts = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ProductParts' AND xtype='U')
                CREATE TABLE ProductParts (
                    ProductID INT           NOT NULL REFERENCES Products(ProductID) ON DELETE CASCADE,
                    PartID    INT           NOT NULL REFERENCES Parts(PartID)       ON DELETE CASCADE,
                    Quantity  DECIMAL(18,4) NOT NULL DEFAULT 1,
                    PRIMARY KEY (ProductID, PartID)
                );";

            public const string ProductPartsDecimalMigration = @"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('ProductParts') AND name = 'Quantity' AND system_type_id = TYPE_ID('int'))
                BEGIN
                    DECLARE @qtyConstraint NVARCHAR(200);
                    SELECT @qtyConstraint = dc.name
                    FROM   sys.default_constraints dc
                    JOIN   sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                    WHERE  c.object_id = OBJECT_ID('ProductParts') AND c.name = 'Quantity';
                    IF @qtyConstraint IS NOT NULL
                        EXEC('ALTER TABLE ProductParts DROP CONSTRAINT [' + @qtyConstraint + ']');
                    ALTER TABLE ProductParts ALTER COLUMN Quantity DECIMAL(18,4) NOT NULL;
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.default_constraints dc
                        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                        WHERE c.object_id = OBJECT_ID('ProductParts') AND c.name = 'Quantity')
                        ALTER TABLE ProductParts ADD DEFAULT 1 FOR Quantity;
                END
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ProductParts') AND name='CreatesBatchLoss')
                    ALTER TABLE ProductParts ADD CreatesBatchLoss BIT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ProductParts') AND name='BatchLossRate')
                    ALTER TABLE ProductParts ADD BatchLossRate DECIMAL(5,2) NOT NULL DEFAULT 0;";

            public const string BomLabourCosts = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='BomLabourCosts' AND xtype='U')
                CREATE TABLE BomLabourCosts (
                    LabourCostID INT IDENTITY(1,1) PRIMARY KEY,
                    ProductID    INT           NOT NULL REFERENCES Products(ProductID) ON DELETE CASCADE,
                    Description  NVARCHAR(100) NOT NULL DEFAULT 'Labour',
                    HourlyRate   DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Hours        DECIMAL(10,2) NOT NULL DEFAULT 1
                );";

            // ── Customers & Sales ─────────────────────────────────────────────
            public const string Customers = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Customers' AND xtype='U')
                CREATE TABLE Customers (
                    CustomerID  INT           IDENTITY(1,1) PRIMARY KEY,
                    Email       NVARCHAR(200) NOT NULL,
                    FullName    NVARCHAR(200) NULL,
                    CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT UQ_Customers_Email UNIQUE (Email)
                );";

            public const string SalesOrders = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='SalesOrders' AND xtype='U')
                CREATE TABLE SalesOrders (
                    SalesOrderID      INT           IDENTITY(1,1) PRIMARY KEY,
                    ShopifyOrderID    BIGINT        NULL,
                    OrderNumber       INT           NOT NULL,
                    CustomerID        INT           NOT NULL REFERENCES Customers(CustomerID),
                    StoreID           INT           NULL     REFERENCES Stores(StoreID),
                    OrderDate         DATETIME      NOT NULL,
                    TotalPrice        DECIMAL(18,2) NOT NULL,
                    Currency          NVARCHAR(10)  NULL,
                    Notes             NVARCHAR(1000) NULL,
                    Status            NVARCHAR(20)  NOT NULL DEFAULT 'Draft',
                    InventoryAffected BIT           NOT NULL DEFAULT 0,
                    OrderType         NVARCHAR(50)  NOT NULL DEFAULT 'Shopify',
                    CreatedAt         DATETIME      NOT NULL DEFAULT GETDATE()
                );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('SalesOrders') AND name='UIX_SalesOrders_ShopifyOrderID')
                    EXEC('CREATE UNIQUE INDEX UIX_SalesOrders_ShopifyOrderID ON SalesOrders(ShopifyOrderID) WHERE ShopifyOrderID IS NOT NULL');";

            public const string SalesOrderItems = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='SalesOrderItems' AND xtype='U')
                CREATE TABLE SalesOrderItems (
                    SalesOrderItemID INT           IDENTITY(1,1) PRIMARY KEY,
                    SalesOrderID     INT           NOT NULL REFERENCES SalesOrders(SalesOrderID),
                    ProductID        INT           NOT NULL REFERENCES Products(ProductID),
                    SKU              NVARCHAR(100) NULL,
                    Title            NVARCHAR(500) NULL,
                    Quantity         INT           NOT NULL,
                    UnitPrice        DECIMAL(18,2) NOT NULL
                );";

            public const string SalesOrderColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='ShippingCost')
                    ALTER TABLE SalesOrders ADD ShippingCost DECIMAL(18,2) NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='IsPaid')
                    ALTER TABLE SalesOrders ADD IsPaid BIT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PaidAt')
                    ALTER TABLE SalesOrders ADD PaidAt DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PaymentGateway')
                    ALTER TABLE SalesOrders ADD PaymentGateway NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PaymentStatus')
                    ALTER TABLE SalesOrders ADD PaymentStatus NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PackedBy')
                    ALTER TABLE SalesOrders ADD PackedBy NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PackedAt')
                    ALTER TABLE SalesOrders ADD PackedAt DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='TrackingNumber')
                    ALTER TABLE SalesOrders ADD TrackingNumber NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='Carrier')
                    ALTER TABLE SalesOrders ADD Carrier NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='ShippedBy')
                    ALTER TABLE SalesOrders ADD ShippedBy NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='ShippedAt')
                    ALTER TABLE SalesOrders ADD ShippedAt DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='CancelledAt')
                    ALTER TABLE SalesOrders ADD CancelledAt DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='DiscountType')
                    ALTER TABLE SalesOrders ADD DiscountType NVARCHAR(20) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='DiscountAmount')
                    ALTER TABLE SalesOrders ADD DiscountAmount DECIMAL(18,2) NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='DiscountPercent')
                    ALTER TABLE SalesOrders ADD DiscountPercent DECIMAL(5,2) NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='OriginalSalesOrderID')
                    ALTER TABLE SalesOrders ADD OriginalSalesOrderID INT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrderItems') AND name='PickedQty')
                    ALTER TABLE SalesOrderItems ADD PickedQty INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrderItems') AND name='PickedBy')
                    ALTER TABLE SalesOrderItems ADD PickedBy NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrderItems') AND name='PickedAt')
                    ALTER TABLE SalesOrderItems ADD PickedAt DATETIME NULL;
                UPDATE SalesOrders SET OrderType='Manual' WHERE ShopifyOrderID IS NULL AND OrderType='Shopify';";

            public const string StockReservations = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='StockReservations' AND xtype='U')
                CREATE TABLE StockReservations (
                    ReservationID INT IDENTITY(1,1) PRIMARY KEY,
                    SalesOrderID  INT NOT NULL REFERENCES SalesOrders(SalesOrderID),
                    ProductID     INT NOT NULL REFERENCES Products(ProductID),
                    LocationID    INT NULL     REFERENCES Locations(LocationID),
                    Quantity      INT NOT NULL,
                    CreatedAt     DATETIME NOT NULL DEFAULT GETDATE()
                );";

            public const string CustomerPayments = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CustomerPayments' AND xtype='U')
                CREATE TABLE CustomerPayments (
                    PaymentID     INT IDENTITY(1,1) PRIMARY KEY,
                    CustomerID    INT           NOT NULL REFERENCES Customers(CustomerID),
                    SalesOrderID  INT           NULL     REFERENCES SalesOrders(SalesOrderID),
                    Amount        DECIMAL(18,2) NOT NULL,
                    PaymentMethod NVARCHAR(50)  NOT NULL DEFAULT 'Cash',
                    Notes         NVARCHAR(500) NULL,
                    PaidAt        DATETIME      NOT NULL DEFAULT GETDATE(),
                    RecordedBy    NVARCHAR(100) NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CustomerPayments') AND name = 'PaymentMethod')
                    ALTER TABLE CustomerPayments ADD PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Cash';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CustomerPayments') AND name = 'RecordedBy')
                    ALTER TABLE CustomerPayments ADD RecordedBy NVARCHAR(100) NULL;";

            public const string CustomerNotes = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CustomerNotes' AND xtype='U')
                CREATE TABLE CustomerNotes (
                    NoteID     INT           NOT NULL IDENTITY PRIMARY KEY,
                    CustomerID INT           NOT NULL,
                    NoteText   NVARCHAR(MAX) NOT NULL,
                    NoteType   NVARCHAR(50)  NOT NULL DEFAULT 'Note',
                    CreatedBy  NVARCHAR(100) NULL,
                    CreatedAt  DATETIME      NOT NULL DEFAULT GETDATE()
                );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_CustomerNotes_CustomerID' AND object_id=OBJECT_ID('CustomerNotes'))
                    CREATE INDEX IX_CustomerNotes_CustomerID ON CustomerNotes (CustomerID) INCLUDE (NoteType, CreatedAt);";

            public const string CustomerCredits = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CustomerCredits' AND xtype='U')
                CREATE TABLE CustomerCredits (
                    CreditID          INT           NOT NULL IDENTITY PRIMARY KEY,
                    CustomerID        INT           NOT NULL,
                    ReturnID          INT           NULL,
                    Amount            DECIMAL(18,2) NOT NULL,
                    CreditType        NVARCHAR(50)  NOT NULL DEFAULT 'Return',
                    Notes             NVARCHAR(500) NULL,
                    IsRedeemed        BIT           NOT NULL DEFAULT 0,
                    RedeemedAt        DATETIME      NULL,
                    RedeemedOnOrderID INT           NULL,
                    CreatedBy         NVARCHAR(100) NULL,
                    CreatedAt         DATETIME      NOT NULL DEFAULT GETDATE()
                );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_CustomerCredits_CustomerID' AND object_id=OBJECT_ID('CustomerCredits'))
                    CREATE INDEX IX_CustomerCredits_CustomerID ON CustomerCredits (CustomerID, IsRedeemed) INCLUDE (Amount, ReturnID, CreatedAt);";

            // ── Suppliers & POs ───────────────────────────────────────────────
            public const string Suppliers = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Suppliers' AND xtype='U')
                CREATE TABLE Suppliers (
                    SupplierID   INT IDENTITY(1,1) PRIMARY KEY,
                    SupplierName NVARCHAR(200) NOT NULL,
                    ContactName  NVARCHAR(200) NULL,
                    Email        NVARCHAR(200) NULL,
                    Phone        NVARCHAR(50)  NULL,
                    Address      NVARCHAR(500) NULL,
                    IsActive     BIT           NOT NULL DEFAULT 1,
                    Notes        NVARCHAR(500) NULL,
                    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
                );";

            public const string PurchaseOrders = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PurchaseOrders' AND xtype='U')
                CREATE TABLE PurchaseOrders (
                    POID         INT IDENTITY(1,1) PRIMARY KEY,
                    PONumber     NVARCHAR(50)  NOT NULL UNIQUE,
                    SupplierID   INT           NOT NULL REFERENCES Suppliers(SupplierID),
                    Status       NVARCHAR(20)  NOT NULL DEFAULT 'Draft',
                    OrderDate    DATETIME      NOT NULL DEFAULT GETDATE(),
                    ExpectedDate DATETIME      NULL,
                    Notes        NVARCHAR(500) NULL,
                    CreatedBy    NVARCHAR(100) NULL,
                    TotalCost    DECIMAL(18,2) NOT NULL DEFAULT 0,
                    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
                );";

            public const string PurchaseOrderItems = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PurchaseOrderItems' AND xtype='U')
                CREATE TABLE PurchaseOrderItems (
                    POItemID         INT IDENTITY(1,1) PRIMARY KEY,
                    POID             INT           NOT NULL REFERENCES PurchaseOrders(POID),
                    PartID           INT           NULL REFERENCES Parts(PartID),
                    ProductID        INT           NULL REFERENCES Products(ProductID),
                    SKU              NVARCHAR(100) NULL,
                    ItemName         NVARCHAR(200) NOT NULL,
                    QuantityOrdered  INT           NOT NULL,
                    QuantityReceived INT           NOT NULL DEFAULT 0,
                    UnitCost         DECIMAL(18,2) NOT NULL DEFAULT 0
                );";

            public const string PurchaseOrderColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PurchaseOrders') AND name='ShippingCost')
                    ALTER TABLE PurchaseOrders ADD ShippingCost DECIMAL(18,2) NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PurchaseOrders') AND name='TaxAmount')
                    ALTER TABLE PurchaseOrders ADD TaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PurchaseOrders') AND name='OverdueNotifiedAt')
                    ALTER TABLE PurchaseOrders ADD OverdueNotifiedAt DATETIME NULL;";

            // ── Manufacturing ─────────────────────────────────────────────────
            public const string ManufacturingOrders = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ManufacturingOrders' AND xtype='U')
                CREATE TABLE ManufacturingOrders (
                    MOID      INT IDENTITY(1,1) PRIMARY KEY,
                    MONumber  NVARCHAR(50)  NOT NULL UNIQUE,
                    Status    NVARCHAR(20)  NOT NULL DEFAULT 'Open',
                    CreatedAt DATETIME      NOT NULL DEFAULT GETDATE(),
                    Notes     NVARCHAR(500) NULL,
                    OrderedBy NVARCHAR(100) NULL
                );";

            public const string WorkOrders = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='WorkOrders' AND xtype='U')
                CREATE TABLE WorkOrders (
                    WorkOrderID    INT IDENTITY(1,1) PRIMARY KEY,
                    MOID           INT           NOT NULL REFERENCES ManufacturingOrders(MOID),
                    ProductID      INT           NOT NULL REFERENCES Products(ProductID),
                    Quantity       INT           NOT NULL,
                    Status         NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
                    Notes          NVARCHAR(500) NULL,
                    CompletedAt    DATETIME      NULL,
                    ShopifyOrderID BIGINT        NULL,
                    CostOfGoods    DECIMAL(18,2) NULL
                );";

            public const string WorkOrderColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'CostOfGoods')
                    ALTER TABLE WorkOrders ADD CostOfGoods DECIMAL(18,2) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'CompletedQty')
                    ALTER TABLE WorkOrders ADD CompletedQty INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'ScrapQty')
                    ALTER TABLE WorkOrders ADD ScrapQty INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'AssignedTo')
                    ALTER TABLE WorkOrders ADD AssignedTo NVARCHAR(100) NULL;";

            public const string PartsReservations = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PartsReservations' AND xtype='U')
                CREATE TABLE PartsReservations (
                    ReservationID INT IDENTITY(1,1) PRIMARY KEY,
                    WorkOrderID   INT NOT NULL REFERENCES WorkOrders(WorkOrderID),
                    PartID        INT NOT NULL REFERENCES Parts(PartID),
                    Quantity      INT NOT NULL,
                    CreatedAt     DATETIME NOT NULL DEFAULT GETDATE()
                );";

            public const string CookSessions = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessions' AND xtype='U')
                CREATE TABLE CookSessions (
                    CookSessionID INT IDENTITY(1,1) PRIMARY KEY,
                    SessionName   NVARCHAR(100) NOT NULL,
                    Status        NVARCHAR(20)  NOT NULL DEFAULT 'Open',
                    CreatedBy     NVARCHAR(100) NULL,
                    CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
                    CompletedAt   DATETIME      NULL
                );";

            public const string CookSessionBatches = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessionBatches' AND xtype='U')
                CREATE TABLE CookSessionBatches (
                    CookSessionID INT NOT NULL REFERENCES CookSessions(CookSessionID) ON DELETE CASCADE,
                    WorkOrderID   INT NOT NULL REFERENCES WorkOrders(WorkOrderID),
                    PRIMARY KEY (CookSessionID, WorkOrderID)
                );";

            public const string CookSessionSteps = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessionSteps' AND xtype='U')
                CREATE TABLE CookSessionSteps (
                    StepID        INT IDENTITY(1,1) PRIMARY KEY,
                    CookSessionID INT NOT NULL REFERENCES CookSessions(CookSessionID) ON DELETE CASCADE,
                    WorkOrderID   INT NOT NULL REFERENCES WorkOrders(WorkOrderID),
                    PartID        INT NOT NULL REFERENCES Parts(PartID),
                    IsDone        BIT           NOT NULL DEFAULT 0,
                    DoneBy        NVARCHAR(100) NULL,
                    DoneAt        DATETIME      NULL,
                    UNIQUE (CookSessionID, WorkOrderID, PartID)
                );";

            public const string CookColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessions') AND name = 'BatchLossPercent')
                    ALTER TABLE CookSessions ADD BatchLossPercent DECIMAL(5,2) NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessionBatches') AND name = 'FlaskType')
                    ALTER TABLE CookSessionBatches ADD FlaskType NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessionBatches') AND name = 'BatchSizeML')
                    ALTER TABLE CookSessionBatches ADD BatchSizeML DECIMAL(12,3) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessionSteps') AND name = 'RequiredQtyML')
                    ALTER TABLE CookSessionSteps ADD RequiredQtyML DECIMAL(12,3) NULL;";

            // ── Tasks ─────────────────────────────────────────────────────────
            public const string TaskWorkflows = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskWorkflows' AND xtype='U')
                CREATE TABLE TaskWorkflows (
                    WorkflowID INT           IDENTITY(1,1) PRIMARY KEY,
                    Name       NVARCHAR(100) NOT NULL
                );";

            public const string TaskWorkflowStatuses = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskWorkflowStatuses' AND xtype='U')
                CREATE TABLE TaskWorkflowStatuses (
                    StatusID   INT           IDENTITY(1,1) PRIMARY KEY,
                    WorkflowID INT           NOT NULL REFERENCES TaskWorkflows(WorkflowID) ON DELETE CASCADE,
                    StatusName NVARCHAR(100) NOT NULL,
                    SortOrder  INT           NOT NULL DEFAULT 0
                );";

            public const string Tasks = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Tasks' AND xtype='U')
                CREATE TABLE Tasks (
                    TaskID      INT           IDENTITY(1,1) PRIMARY KEY,
                    Title       NVARCHAR(200) NOT NULL,
                    Description NVARCHAR(2000) NULL,
                    AssignedTo  NVARCHAR(100) NOT NULL,
                    CreatedBy   NVARCHAR(100) NOT NULL,
                    DueDate     DATETIME      NOT NULL,
                    Status      NVARCHAR(50)  NOT NULL DEFAULT 'Open',
                    CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE()
                );";

            public const string TaskColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'WorkflowID')
                    ALTER TABLE Tasks ADD WorkflowID INT NULL REFERENCES TaskWorkflows(WorkflowID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'WorkflowCurrentStatus')
                    ALTER TABLE Tasks ADD WorkflowCurrentStatus NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'Priority')
                    ALTER TABLE Tasks ADD Priority NVARCHAR(50) NOT NULL DEFAULT 'Normal';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'RecurrencePattern')
                    ALTER TABLE Tasks ADD RecurrencePattern NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'RecurrenceInterval')
                    ALTER TABLE Tasks ADD RecurrenceInterval INT NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'NextOccurrence')
                    ALTER TABLE Tasks ADD NextOccurrence DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'ParentTaskId')
                    ALTER TABLE Tasks ADD ParentTaskId INT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'Tags')
                    ALTER TABLE Tasks ADD Tags NVARCHAR(500) NULL;";

            public const string TaskComments = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskComments' AND xtype='U')
                CREATE TABLE TaskComments (
                    CommentID  INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskID     INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    Username   NVARCHAR(100)  NOT NULL,
                    Body       NVARCHAR(2000) NOT NULL,
                    CreatedAt  DATETIME       NOT NULL DEFAULT GETDATE()
                );";

            public const string TaskMentions = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskMentions' AND xtype='U')
                CREATE TABLE TaskMentions (
                    MentionID     INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskID        INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    MentionedUser NVARCHAR(100)  NOT NULL,
                    MentionedBy   NVARCHAR(100)  NOT NULL,
                    CommentText   NVARCHAR(MAX)  NOT NULL,
                    MentionedAt   DATETIME       NOT NULL DEFAULT GETDATE(),
                    IsRead        BIT            NOT NULL DEFAULT 0
                );";

            public const string TaskLinkedRecords = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskLinkedRecords' AND xtype='U')
                CREATE TABLE TaskLinkedRecords (
                    LinkId        INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskId        INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    LinkedModule  NVARCHAR(50)   NOT NULL,
                    LinkedId      NVARCHAR(100)  NOT NULL,
                    LinkedDisplay NVARCHAR(200)  NULL,
                    CreatedAt     DATETIME       NOT NULL DEFAULT GETDATE()
                );";

            public const string TaskHistory = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskHistory' AND xtype='U')
                CREATE TABLE TaskHistory (
                    HistoryId  INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskId     INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    FieldName  NVARCHAR(100)  NOT NULL,
                    OldValue   NVARCHAR(500)  NULL,
                    NewValue   NVARCHAR(500)  NULL,
                    ChangedBy  NVARCHAR(100)  NOT NULL,
                    ChangedAt  DATETIME       NOT NULL DEFAULT GETDATE()
                );";

            public const string TaskSubtasks = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskSubtasks' AND xtype='U')
                CREATE TABLE TaskSubtasks (
                    SubtaskId   INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskId      INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    Title       NVARCHAR(300)  NOT NULL,
                    IsComplete  BIT            NOT NULL DEFAULT 0,
                    CompletedBy NVARCHAR(100)  NULL,
                    CompletedAt DATETIME       NULL,
                    SortOrder   INT            NOT NULL DEFAULT 0
                );";

            public const string TaskTemplates = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskTemplates' AND xtype='U')
                CREATE TABLE TaskTemplates (
                    TemplateId  INT            IDENTITY(1,1) PRIMARY KEY,
                    Name        NVARCHAR(200)  NOT NULL,
                    Description NVARCHAR(500)  NULL,
                    CreatedBy   NVARCHAR(100)  NULL,
                    CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE()
                );";

            public const string TaskTemplateItems = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskTemplateItems' AND xtype='U')
                CREATE TABLE TaskTemplateItems (
                    ItemId        INT            IDENTITY(1,1) PRIMARY KEY,
                    TemplateId    INT            NOT NULL REFERENCES TaskTemplates(TemplateId) ON DELETE CASCADE,
                    Title         NVARCHAR(200)  NOT NULL,
                    Description   NVARCHAR(1000) NULL,
                    Priority      NVARCHAR(50)   NOT NULL DEFAULT 'Normal',
                    DueDaysOffset INT            NOT NULL DEFAULT 0,
                    SortOrder     INT            NOT NULL DEFAULT 0
                );";

            // ── Accounting ────────────────────────────────────────────────────
            public const string ExpenseCategories = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ExpenseCategories' AND xtype='U')
                CREATE TABLE ExpenseCategories (
                    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
                    Name       NVARCHAR(100) NOT NULL,
                    IsActive   BIT NOT NULL DEFAULT 1
                );";

            public const string Expenses = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Expenses' AND xtype='U')
                CREATE TABLE Expenses (
                    ExpenseID   INT IDENTITY(1,1) PRIMARY KEY,
                    CategoryID  INT           NULL REFERENCES ExpenseCategories(CategoryID),
                    Amount      DECIMAL(18,2) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    ExpenseDate DATETIME      NOT NULL DEFAULT GETDATE(),
                    CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE(),
                    CreatedBy   NVARCHAR(100) NULL
                );";

            public const string TaxRates = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaxRates' AND xtype='U')
                BEGIN
                    CREATE TABLE TaxRates (
                        TaxRateID INT IDENTITY(1,1) PRIMARY KEY,
                        Name      NVARCHAR(100) NOT NULL,
                        Rate      DECIMAL(8,6)  NOT NULL,
                        IsActive  BIT           NOT NULL DEFAULT 1
                    );
                    INSERT INTO TaxRates (Name, Rate) VALUES
                        ('GST',    0.05),
                        ('PST BC', 0.07),
                        ('HST',    0.13);
                END";

            public const string SeedExpenseCategories = @"
                IF NOT EXISTS (SELECT 1 FROM ExpenseCategories)
                BEGIN
                    INSERT INTO ExpenseCategories (Name) VALUES
                        ('Rent / Utilities'),
                        ('Payroll'),
                        ('Supplies'),
                        ('Marketing'),
                        ('Shipping & Logistics'),
                        ('Software & Tools'),
                        ('Other');
                END";

            // ── Packages, Discounts, Returns, Backorders ──────────────────────
            public const string PackageComponents = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PackageComponents' AND xtype='U')
                CREATE TABLE PackageComponents (
                    PackageComponentID INT IDENTITY PRIMARY KEY,
                    PackageProductID   INT NOT NULL REFERENCES Products(ProductID) ON DELETE CASCADE,
                    ComponentProductID INT NOT NULL REFERENCES Products(ProductID),
                    Quantity           INT NOT NULL DEFAULT 1,
                    Notes              NVARCHAR(500) NULL
                );";

            public const string DiscountTiers = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='DiscountTiers' AND xtype='U')
                CREATE TABLE DiscountTiers (
                    TierID          INT IDENTITY PRIMARY KEY,
                    TierName        NVARCHAR(100) NOT NULL,
                    DiscountPercent DECIMAL(5,2)  NOT NULL DEFAULT 0,
                    Description     NVARCHAR(500) NULL,
                    IsActive        BIT           NOT NULL DEFAULT 1
                );";

            public const string DiscountTierColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Customers') AND name='TierID')
                    ALTER TABLE Customers ADD TierID INT NULL REFERENCES DiscountTiers(TierID);";

            public const string ReturnOrders = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ReturnOrders' AND xtype='U')
                CREATE TABLE ReturnOrders (
                    ReturnID        INT           NOT NULL IDENTITY PRIMARY KEY,
                    OriginalOrderID INT           NOT NULL,
                    CustomerID      INT           NOT NULL,
                    ReturnDate      DATE          NOT NULL DEFAULT CAST(GETDATE() AS DATE),
                    Status          NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
                    Reason          NVARCHAR(200) NULL,
                    Notes           NVARCHAR(MAX) NULL,
                    CreatedBy       NVARCHAR(100) NULL,
                    CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE()
                );";

            public const string ReturnOrderItems = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ReturnOrderItems' AND xtype='U')
                CREATE TABLE ReturnOrderItems (
                    ReturnItemID      INT           NOT NULL IDENTITY PRIMARY KEY,
                    ReturnID          INT           NOT NULL,
                    SalesOrderItemID  INT           NULL,
                    ProductID         INT           NOT NULL,
                    SKU               NVARCHAR(100) NULL,
                    ProductName       NVARCHAR(200) NULL,
                    OriginalQty       INT           NOT NULL DEFAULT 0,
                    ReturnQty         INT           NOT NULL DEFAULT 1,
                    OriginalUnitPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Condition         NVARCHAR(20)  NOT NULL DEFAULT 'Resalable',
                    RestockLocationID INT           NULL
                );";

            public const string ReturnColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ReturnOrders') AND name = 'ApprovedBy')
                    ALTER TABLE ReturnOrders ADD ApprovedBy NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ReturnOrders') AND name = 'ApprovedAt')
                    ALTER TABLE ReturnOrders ADD ApprovedAt DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ReturnOrderItems') AND name = 'OriginalUnitPrice')
                    ALTER TABLE ReturnOrderItems ADD OriginalUnitPrice DECIMAL(18,2) NOT NULL DEFAULT 0;";

            public const string Backorders = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Backorders' AND xtype='U')
                CREATE TABLE Backorders (
                    BackorderID      INT          NOT NULL IDENTITY PRIMARY KEY,
                    SalesOrderID     INT          NOT NULL,
                    SalesOrderItemID INT          NOT NULL,
                    ProductID        INT          NOT NULL,
                    BackorderedQty   INT          NOT NULL,
                    FulfilledQty     INT          NOT NULL DEFAULT 0,
                    Status           NVARCHAR(20) NOT NULL DEFAULT 'Open',
                    CreatedAt        DATETIME     NOT NULL DEFAULT GETDATE(),
                    FulfilledAt      DATETIME     NULL
                );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Backorders_ProductID_Status' AND object_id=OBJECT_ID('Backorders'))
                    CREATE INDEX IX_Backorders_ProductID_Status ON Backorders (ProductID, Status) INCLUDE (BackorderedQty, FulfilledQty);";

            // ── Cycle Count columns (Products + LocationCycleSchedule already created above) ──
            public const string CycleCountColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'LastVerifiedAt')
                    ALTER TABLE Products ADD LastVerifiedAt DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'LastVerifiedBy')
                    ALTER TABLE Products ADD LastVerifiedBy NVARCHAR(100) NULL;";

            // ── Shipments / Packing ───────────────────────────────────────────
            public const string BoxTypes = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='BoxTypes' AND xtype='U')
                CREATE TABLE BoxTypes (
                    BoxTypeID INT IDENTITY(1,1) PRIMARY KEY,
                    BoxName   NVARCHAR(100) NOT NULL,
                    Notes     NVARCHAR(500) NULL,
                    IsActive  BIT           NOT NULL DEFAULT 1,
                    CreatedAt DATETIME      NOT NULL DEFAULT GETUTCDATE()
                );";

            public const string Shipments = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Shipments' AND xtype='U')
                CREATE TABLE Shipments (
                    ShipmentID     INT IDENTITY(1,1) PRIMARY KEY,
                    SalesOrderID   INT           NOT NULL REFERENCES SalesOrders(SalesOrderID),
                    BoxTypeID      INT           NULL REFERENCES BoxTypes(BoxTypeID),
                    BoxLabel       NVARCHAR(100) NULL,
                    TrackingNumber NVARCHAR(200) NULL,
                    Carrier        NVARCHAR(100) NULL,
                    Status         NVARCHAR(20)  NOT NULL DEFAULT 'Open',
                    Notes          NVARCHAR(500) NULL,
                    ShippedAt      DATETIME      NULL,
                    ShippedBy      NVARCHAR(100) NULL,
                    CreatedAt      DATETIME      NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy      NVARCHAR(100) NULL
                );";

            public const string ShipmentItems = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ShipmentItems' AND xtype='U')
                CREATE TABLE ShipmentItems (
                    ShipmentItemID   INT IDENTITY(1,1) PRIMARY KEY,
                    ShipmentID       INT NOT NULL REFERENCES Shipments(ShipmentID) ON DELETE CASCADE,
                    SalesOrderItemID INT NOT NULL REFERENCES SalesOrderItems(SalesOrderItemID),
                    Quantity         INT NOT NULL,
                    PackedBy         NVARCHAR(100) NULL,
                    PackedAt         DATETIME      NULL
                );";

            public const string ShipmentColumns = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrderItems') AND name='ShippedQty')
                    ALTER TABLE SalesOrderItems ADD ShippedQty INT NOT NULL DEFAULT 0;";

            // ── Performance & infrastructure ──────────────────────────────────
            public const string AppliedMigrations = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='AppliedMigrations' AND xtype='U')
                CREATE TABLE AppliedMigrations (
                    MigrationName NVARCHAR(200) NOT NULL PRIMARY KEY,
                    AppliedAt     DATETIME      NOT NULL DEFAULT GETDATE()
                );";

            public const string RowVersion = @"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'RowVersion')
                    ALTER TABLE Products ADD RowVersion ROWVERSION NOT NULL;";

            public const string EnableRCSI = @"
                IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()) = 0
                    ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;";

            public const string PerformanceIndexes = @"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_InvTrans_ProductID_LocationID' AND object_id=OBJECT_ID('InventoryTransactions'))
                    CREATE INDEX IX_InvTrans_ProductID_LocationID ON InventoryTransactions (ProductID, LocationID) INCLUDE (QuantityChange, TransactionType, TransactionDate);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_InvTrans_TransactionDate' AND object_id=OBJECT_ID('InventoryTransactions'))
                    CREATE INDEX IX_InvTrans_TransactionDate ON InventoryTransactions (TransactionDate DESC) INCLUDE (ProductID, LocationID, QuantityChange, TransactionType);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_InvTrans_TransactionType' AND object_id=OBJECT_ID('InventoryTransactions'))
                    CREATE INDEX IX_InvTrans_TransactionType ON InventoryTransactions (TransactionType) INCLUDE (ProductID, QuantityChange, TransactionDate);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_StockReservations_ProductID' AND object_id=OBJECT_ID('StockReservations'))
                    CREATE INDEX IX_StockReservations_ProductID ON StockReservations (ProductID) INCLUDE (Quantity);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_IsActive_SKU_Name' AND object_id=OBJECT_ID('Products'))
                    CREATE INDEX IX_Products_IsActive_SKU_Name ON Products (IsActive) INCLUDE (SKU, ProductName, ReorderPoint, LastVerifiedAt);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_LastVerifiedAt' AND object_id=OBJECT_ID('Products'))
                    EXEC('CREATE INDEX IX_Products_LastVerifiedAt ON Products (LastVerifiedAt) WHERE IsActive = 1');
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SalesOrders_Status' AND object_id=OBJECT_ID('SalesOrders'))
                    CREATE INDEX IX_SalesOrders_Status ON SalesOrders (Status) INCLUDE (CustomerID, OrderDate, TotalPrice);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SalesOrders_CustomerID_OrderDate' AND object_id=OBJECT_ID('SalesOrders'))
                    CREATE INDEX IX_SalesOrders_CustomerID_OrderDate ON SalesOrders (CustomerID, OrderDate DESC);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_WorkOrders_ProductID_Status' AND object_id=OBJECT_ID('WorkOrders'))
                    CREATE INDEX IX_WorkOrders_ProductID_Status ON WorkOrders (ProductID, Status);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ProductAttributes_ProductID_Name' AND object_id=OBJECT_ID('ProductAttributes'))
                    CREATE INDEX IX_ProductAttributes_ProductID_Name ON ProductAttributes (ProductID, AttributeName);";
        }
    }
}
