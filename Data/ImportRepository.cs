using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class ImportRepository : IImportRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        /// <summary>
        /// Upserts a product by SKU. New products also get a matching Part and ProductParts entry.
        /// Returns true if inserted, false if updated.
        /// </summary>
        public bool UpsertProduct(string sku, string name, decimal retail, decimal wholesale, int reorder, int stock)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var existing = db.QueryFirstOrDefault<int?>(
                    "SELECT ProductID FROM Products WHERE SKU = @sku", new { sku }, tx);

                if (existing.HasValue)
                {
                    db.Execute(@"
                        UPDATE Products
                        SET ProductName    = @name,
                            RetailPrice    = @retail,
                            WholesalePrice = @wholesale,
                            ReorderPoint   = @reorder
                        WHERE ProductID = @id",
                        new { name, retail, wholesale, reorder, id = existing.Value }, tx);

                    if (stock != 0)
                        db.Execute(@"
                            INSERT INTO InventoryTransactions
                                (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                            VALUES (@id, @stock, 'Adjustment', 'CSV import adjustment', GETDATE())",
                            new { id = existing.Value, stock }, tx);

                    tx.Commit();
                    return false;
                }
                else
                {
                    int newId = db.QuerySingle<int>(@"
                        INSERT INTO Products (SKU, ProductName, RetailPrice, WholesalePrice, ReorderPoint, IsActive, IsAutoCreated)
                        VALUES (@sku, @name, @retail, @wholesale, @reorder, 1, 0);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        new { sku, name, retail, wholesale, reorder }, tx);

                    if (stock > 0)
                        db.Execute(@"
                            INSERT INTO InventoryTransactions
                                (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                            VALUES (@newId, @stock, 'Opening', 'Opening stock (CSV import)', GETDATE())",
                            new { newId, stock }, tx);

                    // Every product must have a matching Part and BOM entry
                    int? partId = db.QueryFirstOrDefault<int?>(
                        "SELECT PartID FROM Parts WHERE PartNumber = @sku", new { sku }, tx);
                    if (partId == null)
                    {
                        partId = db.QuerySingle<int>(@"
                            INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive, IsAutoCreated, IsVerified)
                            VALUES (@sku, @name, 0, 0, 1, 1, 0);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            new { sku, name }, tx);
                    }
                    db.Execute(@"
                        IF NOT EXISTS (SELECT 1 FROM ProductParts WHERE ProductID = @newId AND PartID = @partId)
                        INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@newId, @partId, 1);",
                        new { newId, partId }, tx);

                    tx.Commit();
                    return true;
                }
            }
            catch { tx.Rollback(); throw; }
        }

        public bool UpsertPart(string num, string name, decimal cost, int stock)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var existing = db.QueryFirstOrDefault<int?>(
                "SELECT PartID FROM Parts WHERE PartNumber = @num", new { num });

            if (existing.HasValue)
            {
                db.Execute("UPDATE Parts SET PartName=@name, UnitCost=@cost WHERE PartID=@id",
                    new { name, cost, id = existing.Value });
                if (stock != 0)
                    db.Execute("UPDATE Parts SET CurrentStock = CurrentStock + @stock WHERE PartID=@id",
                        new { stock, id = existing.Value });
                return false;
            }
            else
            {
                db.Execute(@"
                    INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive)
                    VALUES (@num, @name, @cost, @stock, 1)",
                    new { num, name, cost, stock });
                return true;
            }
        }

        public bool UpsertDiscountTier(string name, decimal pct, string desc)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var existing = db.QueryFirstOrDefault<int?>(
                "SELECT TierID FROM DiscountTiers WHERE TierName = @name", new { name });

            if (existing.HasValue)
            {
                db.Execute("UPDATE DiscountTiers SET DiscountPercent=@pct, Description=@desc WHERE TierID=@id",
                    new { pct, desc, id = existing.Value });
                return false;
            }
            else
            {
                db.Execute(@"
                    INSERT INTO DiscountTiers (TierName, DiscountPercent, Description, IsActive)
                    VALUES (@name, @pct, @desc, 1)",
                    new { name, pct, desc });
                return true;
            }
        }

        public bool UpsertCustomer(string email, string fullName, string phone)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var existing = db.QueryFirstOrDefault<int?>(
                "SELECT CustomerID FROM Customers WHERE Email = @email", new { email });

            if (existing.HasValue)
            {
                db.Execute("UPDATE Customers SET CustomerName=@fullName, Phone=@phone WHERE CustomerID=@id",
                    new { fullName, phone, id = existing.Value });
                return false;
            }
            else
            {
                db.Execute(@"
                    INSERT INTO Customers (Email, CustomerName, Phone)
                    VALUES (@email, @fullName, @phone)",
                    new { email, fullName, phone });
                return true;
            }
        }
    }
}
