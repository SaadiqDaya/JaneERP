using System.Data;
using Dapper;
using JaneERP.Core.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Core.Services;

/// <summary>
/// Unified work-order business logic shared by the WinForms desktop app and the REST API.
/// Handles the full lifecycle: GoLive (lot lock) → StartCooking → Complete/Cancel.
/// </summary>
public class WorkOrderService
{
    private readonly string _connectionString;

    public WorkOrderService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private IDbConnection Connect() => new SqlConnection(_connectionString);

    // ── Schema ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Idempotent schema migrations. Call at app startup (both desktop and API).
    /// Each statement is executed independently so a single failure doesn't block the rest.
    /// </summary>
    public static void EnsureSchema(string connectionString)
    {
        using var db = new SqlConnection(connectionString);
        db.Open();

        // PartLots — per-lot inventory for FEFO tracking
        Safe(db, @"
            IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PartLots' AND xtype='U')
            CREATE TABLE PartLots (
                LotID          INT IDENTITY(1,1) PRIMARY KEY,
                PartID         INT           NOT NULL REFERENCES Parts(PartID),
                LocationID     INT           NULL,
                LotNumber      NVARCHAR(100) NULL,
                ExpirationDate DATETIME      NULL,
                Quantity       INT           NOT NULL DEFAULT 0,
                ReceivedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
                Notes          NVARCHAR(500) NULL
            )");

        // PartsReservations — LotID links reservations to specific lots
        Safe(db, @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns
                           WHERE object_id = OBJECT_ID('PartsReservations') AND name = 'LotID')
                ALTER TABLE PartsReservations ADD LotID INT NULL");

        // WorkOrders — LiveAt timestamp for the Go-Live step
        Safe(db, @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns
                           WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'LiveAt')
                ALTER TABLE WorkOrders ADD LiveAt DATETIME NULL");

        // WorkOrders — OutputLocationID records where finished goods were placed
        Safe(db, @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns
                           WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'OutputLocationID')
                ALTER TABLE WorkOrders ADD OutputLocationID INT NULL");
    }

    private static void Safe(IDbConnection db, string sql)
    {
        try { db.Execute(sql); } catch { /* migration already ran */ }
    }

    // ── Lot Availability ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns BOM parts with their available lots sorted FEFO (earliest expiry first,
    /// null-expiry lots last). Parts that have no PartLots rows return a single "unlotted"
    /// synthetic row for backward compatibility with existing stock.
    /// </summary>
    public List<PartLotAvailability> GetLotAvailability(int workOrderId)
    {
        using var db = Connect();

        var bomRows = db.Query(@"
            SELECT pp.PartID, pt.PartNumber, pt.PartName,
                   CAST(CEILING(pp.Quantity * wo.Quantity *
                       (CASE WHEN pp.CreatesBatchLoss = 1 AND pp.BatchLossRate > 0
                             THEN 1.0 + pp.BatchLossRate / 100.0
                             ELSE 1.0 END)) AS INT) AS Required
            FROM   WorkOrders   wo
            JOIN   ProductParts pp ON pp.ProductID = wo.ProductID
            JOIN   Parts        pt ON pt.PartID    = pp.PartID
            WHERE  wo.WorkOrderID = @workOrderId
            ORDER  BY pt.PartName",
            new { workOrderId }).ToList();

        var result = new List<PartLotAvailability>();

        foreach (var bom in bomRows)
        {
            int partId   = (int)bom.PartID;
            int required = (int)bom.Required;

            var lots = db.Query(@"
                SELECT pl.LotID, pl.LotNumber, pl.ExpirationDate, pl.LocationID, pl.Quantity,
                       ISNULL(l.LocationName, 'No Location') AS LocationName,
                       ISNULL((
                           SELECT SUM(pr.Quantity)
                           FROM   PartsReservations pr
                           WHERE  pr.LotID        = pl.LotID
                             AND  pr.WorkOrderID <> @workOrderId
                       ), 0) AS AlreadyReserved
                FROM   PartLots  pl
                LEFT JOIN Locations l ON l.LocationID = pl.LocationID
                WHERE  pl.PartID = @partId
                  AND  pl.Quantity > 0
                ORDER BY
                    CASE WHEN pl.ExpirationDate IS NULL THEN 1 ELSE 0 END,
                    pl.ExpirationDate ASC,
                    pl.LotID ASC",
                new { partId, workOrderId }).ToList();

            List<LotAvailabilityRow> lotRows;

            if (lots.Count > 0)
            {
                lotRows = lots.Select(l => new LotAvailabilityRow
                {
                    LotID           = (int)l.LotID,
                    LotNumber       = (string?)l.LotNumber,
                    ExpirationDate  = (DateTime?)l.ExpirationDate,
                    LocationID      = (int?)l.LocationID,
                    LocationName    = (string)l.LocationName,
                    TotalQty        = (int)l.Quantity,
                    AlreadyReserved = (int)l.AlreadyReserved,
                    Available       = Math.Max(0, (int)l.Quantity - (int)l.AlreadyReserved),
                }).ToList();
            }
            else
            {
                // No PartLots rows — fall back to global CurrentStock (unlotted)
                int stock = db.QuerySingle<int>(
                    "SELECT CurrentStock FROM Parts WHERE PartID = @partId", new { partId });

                int alreadyReserved = db.ExecuteScalar<int>(@"
                    SELECT ISNULL(SUM(pr.Quantity), 0)
                    FROM   PartsReservations pr
                    WHERE  pr.PartID        = @partId
                      AND  pr.WorkOrderID <> @workOrderId
                      AND  pr.LotID IS NULL",
                    new { partId, workOrderId });

                lotRows =
                [
                    new LotAvailabilityRow
                    {
                        LotID           = 0,
                        LotNumber       = null,
                        ExpirationDate  = null,
                        LocationID      = null,
                        LocationName    = "Unlotted",
                        TotalQty        = stock,
                        AlreadyReserved = alreadyReserved,
                        Available       = Math.Max(0, stock - alreadyReserved),
                    }
                ];
            }

            result.Add(new PartLotAvailability
            {
                PartID     = partId,
                PartNumber = (string)bom.PartNumber,
                PartName   = (string)bom.PartName,
                Required   = required,
                Lots       = lotRows,
            });
        }

        return result;
    }

    // ── Go Live ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Locks inventory lots for a work order and transitions it Pending → Live.
    /// The reservation choices are persisted so completion can deduct the correct lots.
    /// </summary>
    public void GoLive(int workOrderId, IEnumerable<LotReservation> reservations, string username)
    {
        using var db = new SqlConnection(_connectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var status = db.ExecuteScalar<string?>(
                "SELECT Status FROM WorkOrders WHERE WorkOrderID = @workOrderId",
                new { workOrderId }, tx)
                ?? throw new InvalidOperationException($"Work order {workOrderId} not found.");

            if (status != "Pending" && status != "Draft")
                throw new InvalidOperationException(
                    $"Cannot go live: work order is already '{status}'.");

            // Replace any prior reservations for this WO
            db.Execute("DELETE FROM PartsReservations WHERE WorkOrderID = @workOrderId",
                new { workOrderId }, tx);

            foreach (var r in reservations.Where(r => r.Quantity > 0))
            {
                if (r.LotID > 0)
                {
                    db.Execute(@"
                        INSERT INTO PartsReservations (WorkOrderID, PartID, Quantity, LotID)
                        VALUES (@workOrderId, @partId, @qty, @lotId)",
                        new { workOrderId, partId = r.PartID, qty = r.Quantity, lotId = r.LotID }, tx);
                }
                else
                {
                    // Unlotted — backward compat (no LotID)
                    db.Execute(@"
                        INSERT INTO PartsReservations (WorkOrderID, PartID, Quantity)
                        VALUES (@workOrderId, @partId, @qty)",
                        new { workOrderId, partId = r.PartID, qty = r.Quantity }, tx);
                }
            }

            db.Execute(@"
                UPDATE WorkOrders SET Status = 'Live', LiveAt = GETDATE()
                WHERE  WorkOrderID = @workOrderId",
                new { workOrderId }, tx);

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Start Cooking ─────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions a work order Live → InProgress when a cook session begins.
    /// Also accepts Pending WOs for backward compatibility (skips the lot-lock step).
    /// Idempotent if already InProgress.
    /// </summary>
    public void StartCooking(int workOrderId, string username)
    {
        using var db = Connect();

        var status = db.ExecuteScalar<string?>(
            "SELECT Status FROM WorkOrders WHERE WorkOrderID = @workOrderId",
            new { workOrderId })
            ?? throw new InvalidOperationException($"Work order {workOrderId} not found.");

        if (status == "InProgress") return; // idempotent

        if (status != "Live" && status != "Pending")
            throw new InvalidOperationException(
                $"Cannot start cooking: work order is '{status}' (expected Live or Pending).");

        db.Execute(
            "UPDATE WorkOrders SET Status = 'InProgress' WHERE WorkOrderID = @workOrderId",
            new { workOrderId });
    }

    // ── Complete ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Atomically completes a work order. Operations (in order):
    ///  1. Calculates BOM deduction quantities (with batch loss)
    ///  2. Pre-checks Parts.CurrentStock has enough
    ///  3. Deducts consumed quantities from reserved PartLots
    ///  4. Decrements Parts.CurrentStock (denormalised global total)
    ///  5. Inserts finished-goods InventoryTransaction at the chosen location
    ///  6. Calculates and records COGS
    ///  7. Releases PartsReservations
    ///  8. Marks WO Complete (records CompletedQty, ScrapQty, OutputLocationID)
    ///  9. Cascades to parent ManufacturingOrder and linked SalesOrders
    /// </summary>
    public void Complete(int workOrderId, CompleteWorkOrderRequest req, string username)
    {
        using var db = new SqlConnection(_connectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var wo = db.QueryFirstOrDefault(@"
                SELECT WorkOrderID, ProductID, Quantity, Status, MOID
                FROM   WorkOrders
                WHERE  WorkOrderID = @workOrderId",
                new { workOrderId }, tx)
                ?? throw new InvalidOperationException($"Work order {workOrderId} not found.");

            int productId    = (int)wo.ProductID;
            int completedQty = req.CompletedQty;
            int scrapQty     = req.ScrapQty;
            int totalDone    = completedQty + scrapQty;
            var now          = DateTime.Now;

            // ── 1. BOM deduction map ─────────────────────────────────────────

            var bom = db.Query(@"
                SELECT pp.PartID, pp.Quantity AS BomQty, pp.CreatesBatchLoss, pp.BatchLossRate
                FROM   ProductParts pp
                WHERE  pp.ProductID = @productId",
                new { productId }, tx).ToList();

            var deductMap = bom.ToDictionary(
                b => (int)b.PartID,
                b =>
                {
                    decimal effRate = (bool)b.CreatesBatchLoss && (decimal)b.BatchLossRate > 0m
                        ? (decimal)b.BatchLossRate : 0m;
                    return (int)Math.Ceiling(
                        (double)((decimal)b.BomQty * totalDone * (1m + effRate / 100m)));
                });

            // ── 2. Stock pre-check ───────────────────────────────────────────

            foreach (var (partId, deductQty) in deductMap)
            {
                if (deductQty <= 0) continue;
                int stock = db.QuerySingle<int>(
                    "SELECT CurrentStock FROM Parts WHERE PartID = @partId", new { partId }, tx);
                if (stock < deductQty)
                {
                    string nm = db.ExecuteScalar<string>(
                        "SELECT PartName FROM Parts WHERE PartID = @partId", new { partId }, tx)
                        ?? $"PartID {partId}";
                    throw new InvalidOperationException(
                        $"Insufficient stock for '{nm}': need {deductQty}, have {stock}.");
                }
            }

            // ── 3. Deduct reserved PartLots ──────────────────────────────────

            var reservations = db.Query(@"
                SELECT pr.PartID, ISNULL(pr.LotID, 0) AS LotID, pr.Quantity
                FROM   PartsReservations pr
                WHERE  pr.WorkOrderID = @workOrderId",
                new { workOrderId }, tx).ToList();

            foreach (var res in reservations)
            {
                int lotId = (int)res.LotID;
                if (lotId <= 0) continue; // unlotted: no PartLots row to update

                int partId    = (int)res.PartID;
                int deductQty = deductMap.GetValueOrDefault(partId, 0);
                int lotDeduct = Math.Min(deductQty, (int)res.Quantity);

                if (lotDeduct > 0)
                {
                    db.Execute(@"
                        UPDATE PartLots
                        SET    Quantity = CASE WHEN Quantity <= @lotDeduct THEN 0
                                              ELSE Quantity - @lotDeduct END
                        WHERE  LotID = @lotId",
                        new { lotDeduct, lotId }, tx);
                }
            }

            // ── 4. Decrement Parts.CurrentStock ──────────────────────────────

            foreach (var (partId, deductQty) in deductMap)
            {
                if (deductQty > 0)
                    db.Execute(
                        "UPDATE Parts SET CurrentStock = CurrentStock - @deductQty WHERE PartID = @partId",
                        new { deductQty, partId }, tx);
            }

            // ── 5. Resolve finished-goods output location ────────────────────

            int? locationId = req.LocationID;
            if (locationId == null)
            {
                locationId = db.ExecuteScalar<int?>(
                    "SELECT DefaultLocationID FROM Products WHERE ProductID = @productId",
                    new { productId }, tx);
            }
            if (locationId == null)
            {
                locationId = db.ExecuteScalar<int?>(
                    "SELECT TOP 1 LocationID FROM Locations ORDER BY LocationID", null, tx);
            }

            // ── 6. Finished-goods inventory transaction ──────────────────────

            if (completedQty > 0)
            {
                string txNotes = $"WO#{workOrderId} completed — {completedQty} units" +
                                 (scrapQty > 0 ? $" ({scrapQty} scrapped)" : "") +
                                 (string.IsNullOrWhiteSpace(req.Notes) ? "" : $" — {req.Notes}");
                db.Execute(@"
                    INSERT INTO InventoryTransactions
                        (ProductID, LocationID, QuantityChange, TransactionType, Notes, TransactionDate)
                    VALUES (@productId, @locationId, @completedQty, 'ManufacturingIn', @txNotes, @now)",
                    new { productId, locationId, completedQty, txNotes, now }, tx);
            }

            // ── 7. COGS ──────────────────────────────────────────────────────

            var bomCost = db.Query(@"
                SELECT pp.Quantity AS BomQty, ISNULL(p.UnitCost, 0) AS UnitCost
                FROM   ProductParts pp
                JOIN   Parts        p ON p.PartID = pp.PartID
                WHERE  pp.ProductID = @productId",
                new { productId }, tx);

            var labour = db.Query(@"
                SELECT HourlyRate, Hours
                FROM   BomLabourCosts
                WHERE  ProductID = @productId",
                new { productId }, tx);

            decimal totalCogs =
                bomCost.Sum(b => (decimal)b.UnitCost * (decimal)b.BomQty * totalDone) +
                labour.Sum(l => (decimal)l.HourlyRate * (decimal)l.Hours * totalDone);

            // ── 8. Release reservations & mark WO complete ───────────────────

            db.Execute("DELETE FROM PartsReservations WHERE WorkOrderID = @workOrderId",
                new { workOrderId }, tx);

            db.Execute(@"
                UPDATE WorkOrders
                SET    Status           = 'Complete',
                       CompletedAt      = @now,
                       CompletedQty     = @completedQty,
                       ScrapQty         = @scrapQty,
                       CostOfGoods      = @cogs,
                       OutputLocationID = @locationId,
                       Notes            = ISNULL(@extraNotes, Notes)
                WHERE  WorkOrderID = @workOrderId",
                new
                {
                    workOrderId, now, completedQty, scrapQty,
                    cogs       = totalCogs,
                    locationId,
                    extraNotes = string.IsNullOrWhiteSpace(req.Notes) ? (string?)null : req.Notes
                }, tx);

            // ── 9. Cascade to ManufacturingOrder and SalesOrders ─────────────

            int moid = (int)wo.MOID;
            int remaining = db.QuerySingle<int>(@"
                SELECT COUNT(*)
                FROM   WorkOrders
                WHERE  MOID   = @moid
                  AND  Status NOT IN ('Complete', 'Cancelled')",
                new { moid }, tx);

            if (remaining == 0)
            {
                db.Execute("UPDATE ManufacturingOrders SET Status = 'Complete' WHERE MOID = @moid",
                    new { moid }, tx);

                db.Execute(@"
                    UPDATE SalesOrders
                    SET    Status = 'Complete'
                    WHERE  Status <> 'Complete'
                      AND  ShopifyOrderID IN (
                            SELECT wo1.ShopifyOrderID
                            FROM   WorkOrders wo1
                            WHERE  wo1.MOID = @moid AND wo1.ShopifyOrderID IS NOT NULL
                              AND  NOT EXISTS (
                                    SELECT 1 FROM WorkOrders wo2
                                    WHERE  wo2.ShopifyOrderID = wo1.ShopifyOrderID
                                      AND  wo2.Status NOT IN ('Complete', 'Cancelled')
                                   )
                           )",
                    new { moid }, tx);
            }

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    /// <summary>Releases all lot reservations and marks the work order Cancelled.</summary>
    public void Cancel(int workOrderId, string username)
    {
        using var db = new SqlConnection(_connectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var status = db.ExecuteScalar<string?>(
                "SELECT Status FROM WorkOrders WHERE WorkOrderID = @workOrderId",
                new { workOrderId }, tx)
                ?? throw new InvalidOperationException($"Work order {workOrderId} not found.");

            if (status == "Complete")
                throw new InvalidOperationException("Cannot cancel a completed work order.");

            db.Execute("DELETE FROM PartsReservations WHERE WorkOrderID = @workOrderId",
                new { workOrderId }, tx);

            db.Execute("UPDATE WorkOrders SET Status = 'Cancelled' WHERE WorkOrderID = @workOrderId",
                new { workOrderId }, tx);

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    // ── BOM Preview ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns BOM ingredients with required quantities (batch-loss-adjusted) for a WO.
    /// </summary>
    public List<BomPreviewRow> GetBomPreview(int workOrderId)
    {
        using var db = Connect();
        return db.Query<BomPreviewRow>(@"
            SELECT pt.PartID, pt.PartNumber, pt.PartName,
                   ISNULL(pt.UnitOfMeasure, '') AS UOM,
                   CAST(CEILING(pp.Quantity * wo.Quantity *
                       CASE WHEN pp.CreatesBatchLoss = 1 AND pp.BatchLossRate > 0
                            THEN 1.0 + pp.BatchLossRate / 100.0 ELSE 1.0 END
                   ) AS INT)               AS RequiredQty,
                   pt.CurrentStock         AS OnHand,
                   pp.CreatesBatchLoss,
                   pp.BatchLossRate
            FROM   WorkOrders   wo
            JOIN   ProductParts pp ON pp.ProductID = wo.ProductID
            JOIN   Parts        pt ON pt.PartID    = pp.PartID
            WHERE  wo.WorkOrderID = @workOrderId
            ORDER  BY pt.PartName",
            new { workOrderId }).ToList();
    }
}
