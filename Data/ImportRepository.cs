using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
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

        public List<InventoryMoveRow> ValidateInventoryMoves(
            IEnumerable<(string sku, string from, string to, int? qty)> input)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            var result = new List<InventoryMoveRow>();

            foreach (var (sku, from, to, qty) in input)
            {
                var row = new InventoryMoveRow
                {
                    SKU          = sku,
                    FromLocation = from,
                    ToLocation   = to,
                    RequestedQty = qty
                };

                // Resolve product
                var product = db.QueryFirstOrDefault(
                    "SELECT ProductID, ProductName FROM Products WHERE SKU = @sku AND IsActive = 1",
                    new { sku });
                if (product == null)
                {
                    row.IsValid = false;
                    row.Error   = $"SKU '{sku}' not found";
                    result.Add(row);
                    continue;
                }
                row.ProductID   = (int)product.ProductID;
                row.ProductName = (string)product.ProductName;

                // Resolve from-location
                var fromLocId = db.QueryFirstOrDefault<int?>(
                    "SELECT LocationID FROM Locations WHERE LocationName = @name", new { name = from });
                if (fromLocId == null)
                {
                    row.IsValid = false;
                    row.Error   = $"Location '{from}' not found";
                    result.Add(row);
                    continue;
                }
                row.FromLocID = fromLocId.Value;

                // Resolve to-location
                var toLocId = db.QueryFirstOrDefault<int?>(
                    "SELECT LocationID FROM Locations WHERE LocationName = @name", new { name = to });
                if (toLocId == null)
                {
                    row.IsValid = false;
                    row.Error   = $"Location '{to}' not found";
                    result.Add(row);
                    continue;
                }
                row.ToLocID = toLocId.Value;

                if (row.FromLocID == row.ToLocID)
                {
                    row.IsValid = false;
                    row.Error   = "Source and destination are the same";
                    result.Add(row);
                    continue;
                }

                // Available stock at source
                int available = db.QuerySingle<int>(
                    @"SELECT ISNULL(SUM(QuantityChange), 0)
                      FROM   InventoryTransactions
                      WHERE  ProductID  = @pid
                        AND  LocationID = @locId",
                    new { pid = row.ProductID, locId = row.FromLocID });
                row.AvailableQty = available;

                if (available <= 0)
                {
                    row.IsValid = false;
                    row.Error   = "No stock at source location";
                    result.Add(row);
                    continue;
                }

                row.MoveQty = Math.Min(qty ?? available, available);
                row.IsValid = true;

                // Summarise available lots for display in the preview grid
                var lots = db.Query(
                    @"SELECT LotNumber, ExpirationDate, SUM(QuantityChange) AS LotQty
                      FROM   InventoryTransactions
                      WHERE  ProductID  = @pid AND LocationID = @locId
                      GROUP BY LotNumber, ExpirationDate
                      HAVING SUM(QuantityChange) > 0
                      ORDER BY ExpirationDate ASC",
                    new { pid = row.ProductID, locId = row.FromLocID });

                row.LotSummary = string.Join(", ", lots.Select(l =>
                {
                    string lotLabel = ((string?)l.LotNumber) != null ? (string)l.LotNumber : "no-lot";
                    string expLabel = ((DateTime?)l.ExpirationDate) != null
                        ? $" (exp {((DateTime)l.ExpirationDate):yyyy-MM-dd})"
                        : string.Empty;
                    return lotLabel + expLabel;
                }));

                result.Add(row);
            }

            return result;
        }

        public (int moved, int skipped) ExecuteInventoryMoves(
            IEnumerable<InventoryMoveRow> validRows, string movedBy)
        {
            int moved = 0, skipped = 0;
            using var db = new SqlConnection(_connectionString);
            db.Open();

            foreach (var row in validRows.Where(r => r.IsValid && r.MoveQty > 0))
            {
                using var tx = db.BeginTransaction();
                try
                {
                    // Query lots in FEFO order (soonest expiry first; null expiry last).
                    // If row.LotNumber is set, restrict to that lot only.
                    var lots = db.Query(
                        @"SELECT   LotNumber, ExpirationDate, SUM(QuantityChange) AS LotQty
                          FROM     InventoryTransactions
                          WHERE    ProductID  = @pid
                            AND    LocationID = @fromLoc
                            AND    (@lotFilter IS NULL OR LotNumber = @lotFilter)
                          GROUP BY LotNumber, ExpirationDate
                          HAVING   SUM(QuantityChange) > 0
                          ORDER BY CASE WHEN ExpirationDate IS NULL THEN 1 ELSE 0 END,
                                   ExpirationDate ASC,
                                   LotNumber ASC",
                        new
                        {
                            pid       = row.ProductID,
                            fromLoc   = row.FromLocID,
                            lotFilter = row.LotNumber   // null = no filter, any lot
                        }, tx).ToList();

                    int remaining = row.MoveQty;

                    foreach (var lot in lots)
                    {
                        if (remaining <= 0) break;

                        int lotQty = (int)lot.LotQty;
                        int take   = Math.Min(lotQty, remaining);

                        string? lotNum  = (string?)lot.LotNumber;
                        DateTime? expDt = (DateTime?)lot.ExpirationDate;

                        // Debit source
                        db.Execute(@"
                            INSERT INTO InventoryTransactions
                                (ProductID, LocationID, QuantityChange, TransactionType,
                                 Notes, TransactionDate, LotNumber, ExpirationDate)
                            VALUES (@pid, @fromLoc, @negQty, 'Transfer',
                                    @notes, GETDATE(), @lotNum, @expDt)",
                            new
                            {
                                pid     = row.ProductID,
                                fromLoc = row.FromLocID,
                                negQty  = -take,
                                notes   = $"Transfer to {row.ToLocation} by {movedBy}",
                                lotNum,
                                expDt
                            }, tx);

                        // Credit destination
                        db.Execute(@"
                            INSERT INTO InventoryTransactions
                                (ProductID, LocationID, QuantityChange, TransactionType,
                                 Notes, TransactionDate, LotNumber, ExpirationDate)
                            VALUES (@pid, @toLoc, @posQty, 'Transfer',
                                    @notes, GETDATE(), @lotNum, @expDt)",
                            new
                            {
                                pid    = row.ProductID,
                                toLoc  = row.ToLocID,
                                posQty = take,
                                notes  = $"Transfer from {row.FromLocation} by {movedBy}",
                                lotNum,
                                expDt
                            }, tx);

                        remaining -= take;
                    }

                    tx.Commit();
                    moved++;
                }
                catch (Exception ex) { tx.Rollback(); Logging.AppLogger.Error($"[ImportRepository.MoveInventoryFromCsv] Row skipped: {ex}"); skipped++; }
            }

            AppLogger.Audit(movedBy, "InventoryMove",
                $"Transferred {moved} product-location(s) via CSV import");

            return (moved, skipped);
        }
    }
}
