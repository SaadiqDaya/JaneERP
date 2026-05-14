using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiPurchaseOrderRepository
{
    private readonly CompanyContext                       _ctx;
    private readonly ILogger<ApiPurchaseOrderRepository> _logger;

    public ApiPurchaseOrderRepository(CompanyContext ctx, ILogger<ApiPurchaseOrderRepository> logger)
    {
        _ctx    = ctx;
        _logger = logger;
    }

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public List<PurchaseOrderListItem> GetOrders(string? statusFilter, string? q = null)
    {
        using var db = Connect();
        var statusClause = statusFilter switch
        {
            "active"   => "po.Status IN ('Draft', 'Sent', 'PartiallyReceived')",
            "pending"  => "po.Status IN ('Sent', 'PartiallyReceived')",
            null or "" => null,
            _          => "po.Status = @statusFilter"
        };
        var qClause = !string.IsNullOrEmpty(q)
            ? "(s.SupplierName LIKE @q OR po.PONumber LIKE @q)"
            : null;

        var parts = new[] { statusClause, qClause }.Where(x => x != null);
        var where = parts.Any() ? "WHERE " + string.Join(" AND ", parts) : "";

        return db.Query<PurchaseOrderListItem>($@"
            SELECT  po.POID, po.PONumber, s.SupplierName,
                    po.Status, po.OrderDate, po.ExpectedDate,
                    po.TotalCost, po.CreatedBy,
                    CASE WHEN po.ExpectedDate IS NOT NULL
                              AND po.ExpectedDate < GETDATE()
                              AND po.Status NOT IN ('Received', 'Cancelled')
                         THEN 1 ELSE 0 END AS IsOverdue
            FROM    PurchaseOrders po
            JOIN    Suppliers s ON s.SupplierID = po.SupplierID
            {where}
            ORDER   BY po.CreatedAt DESC",
            new { statusFilter, q = string.IsNullOrEmpty(q) ? null : $"%{q}%" }).ToList();
    }

    public PurchaseOrderDetail? GetOrderDetail(int poid)
    {
        using var db = Connect();

        // Try with all optional columns (post-migration); fall back to safe query if columns missing
        PurchaseOrderDetail? po;
        try
        {
            po = db.QueryFirstOrDefault<PurchaseOrderDetail>(@"
                SELECT  po.POID, po.PONumber, s.SupplierName,
                        po.Status, po.OrderDate, po.ExpectedDate,
                        po.TotalCost,
                        po.ShippingCost,
                        ISNULL(po.CreatedBy, '') AS CreatedBy,
                        ISNULL(po.Notes,     '') AS Notes,
                        CASE WHEN po.ExpectedDate IS NOT NULL
                                  AND po.ExpectedDate < GETDATE()
                                  AND po.Status NOT IN ('Received', 'Cancelled')
                             THEN 1 ELSE 0 END AS IsOverdue
                FROM    PurchaseOrders po
                JOIN    Suppliers s ON s.SupplierID = po.SupplierID
                WHERE   po.POID = @poid",
                new { poid });
        }
        catch (Exception ex)
        {
            // One or more optional columns don't exist yet — query with safe defaults
            _logger.LogDebug(ex, "[ApiPurchaseOrderRepository.GetOrderDetail] Full query failed for POID={Id}, falling back to safe defaults", poid);
            po = db.QueryFirstOrDefault<PurchaseOrderDetail>(@"
                SELECT  po.POID, po.PONumber, s.SupplierName,
                        po.Status, po.OrderDate, po.ExpectedDate,
                        po.TotalCost,
                        0  AS ShippingCost,
                        '' AS CreatedBy,
                        '' AS Notes,
                        CASE WHEN po.ExpectedDate IS NOT NULL
                                  AND po.ExpectedDate < GETDATE()
                                  AND po.Status NOT IN ('Received', 'Cancelled')
                             THEN 1 ELSE 0 END AS IsOverdue
                FROM    PurchaseOrders po
                JOIN    Suppliers s ON s.SupplierID = po.SupplierID
                WHERE   po.POID = @poid",
                new { poid });
        }

        if (po == null) return null;

        try
        {
            po.Items = db.Query<PoLineItem>(@"
                SELECT POItemID, PartID, ProductID, SKU, ItemName,
                       QuantityOrdered, QuantityReceived, UnitCost
                FROM   PurchaseOrderItems
                WHERE  POID = @poid
                ORDER  BY POItemID",
                new { poid }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[ApiPurchaseOrderRepository.GetOrderDetail] PurchaseOrderItems query failed for POID={Id}, returning empty items", poid);
            po.Items = [];
        }

        return po;
    }

    public void ReceiveItems(int poid, List<ReceiveItem> receivals, string username)
    {
        using var db = new SqlConnection(_ctx.ConnectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            foreach (var r in receivals)
            {
                if (r.QtyReceived <= 0) continue;

                var item = db.QueryFirstOrDefault<PoLineItem>(
                    "SELECT * FROM PurchaseOrderItems WHERE POItemID = @id",
                    new { id = r.PoItemId }, tx);
                if (item == null) continue;

                // Cap received at ordered quantity
                var maxReceivable = item.QuantityOrdered - item.QuantityReceived;
                var qty           = Math.Min(r.QtyReceived, maxReceivable);
                if (qty <= 0) continue;

                db.Execute(@"
                    UPDATE PurchaseOrderItems
                    SET    QuantityReceived = QuantityReceived + @qty
                    WHERE  POItemID = @id",
                    new { qty, id = r.PoItemId }, tx);

                // Update Parts stock
                if (item.PartID.HasValue)
                {
                    db.Execute(
                        "UPDATE Parts SET CurrentStock = CurrentStock + @qty WHERE PartID = @partId",
                        new { qty, partId = item.PartID.Value }, tx);
                }

                // Create InventoryTransaction for product-linked items.
                // Parts (PartID only, no ProductID) use the CurrentStock column instead and do NOT get a ledger row.
                if (item.ProductID.HasValue)
                {
                    // Resolve the PO number for the audit note
                    var poNumber = db.ExecuteScalar<string>(
                        "SELECT PONumber FROM PurchaseOrders WHERE POID = @poid",
                        new { poid }, tx) ?? poid.ToString();

                    // Use the product's default location; fall back to the first location if unset
                    var locationId = db.ExecuteScalar<int?>(@"
                        SELECT DefaultLocationID FROM Products WHERE ProductID = @pid",
                        new { pid = item.ProductID.Value }, tx)
                        ?? db.ExecuteScalar<int?>(@"
                        SELECT TOP 1 LocationID FROM Locations ORDER BY LocationID",
                        null, tx);

                    db.Execute(@"
                        INSERT INTO InventoryTransactions
                            (ProductID, LocationID, QuantityChange, TransactionType, Notes, TransactionDate)
                        VALUES
                            (@pid, @locationId, @qty, 'PurchaseOrder',
                             CONCAT('PO# ', @poNumber, ' received'), GETDATE())",
                        new
                        {
                            pid        = item.ProductID.Value,
                            locationId,
                            qty,
                            poNumber
                        }, tx);
                }
            }

            // Update PO status
            var allItems = db.Query<(int Ordered, int Received)>(
                "SELECT QuantityOrdered, QuantityReceived FROM PurchaseOrderItems WHERE POID = @poid",
                new { poid }, tx).ToList();

            var newStatus = allItems.All(i => i.Received >= i.Ordered)  ? "Received"
                          : allItems.Any(i => i.Received > 0)           ? "PartiallyReceived"
                                                                         : "Sent";
            db.Execute(
                "UPDATE PurchaseOrders SET Status = @s WHERE POID = @poid",
                new { s = newStatus, poid }, tx);

            tx.Commit();

            try
            {
                var receivedItems = receivals.Where(r => r.QtyReceived > 0)
                    .Select(r => $"POItemID={r.PoItemId} Qty={r.QtyReceived}");
                db.Execute(@"
                    INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
                    VALUES (@user, 'ReceivePOItems', @details, GETDATE())",
                    new
                    {
                        user    = username,
                        details = $"POID={poid} NewStatus={newStatus} Items=[{string.Join(", ", receivedItems)}]"
                    });
            }
            catch (Exception auditEx) { _logger.LogError(auditEx, "[ApiPurchaseOrderRepository.ReceiveItems] Audit insert failed for POID={Id}", poid); }
        }
        catch { tx.Rollback(); throw; }
    }

    public int GetItemsToReceiveCount()
    {
        using var db = Connect();
        return db.ExecuteScalar<int>(@"
            SELECT COUNT(*)
            FROM   PurchaseOrderItems poi
            JOIN   PurchaseOrders po ON po.POID = poi.POID
            WHERE  poi.QuantityReceived < poi.QuantityOrdered
              AND  po.Status NOT IN ('Received', 'Cancelled')");
    }

    public int GetOverduePOCount()
    {
        using var db = Connect();
        return db.ExecuteScalar<int>(@"
            SELECT COUNT(*)
            FROM   PurchaseOrders
            WHERE  ExpectedDate IS NOT NULL
              AND  ExpectedDate < GETDATE()
              AND  Status NOT IN ('Received', 'Cancelled')");
    }

    public int DuplicatePO(int sourcePoid, string createdBy)
    {
        using var db = new SqlConnection(_ctx.ConnectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var source = db.QueryFirstOrDefault(
                "SELECT SupplierID, ISNULL(ShippingCost,0) AS ShippingCost, Notes FROM PurchaseOrders WHERE POID = @poid",
                new { poid = sourcePoid }, tx)
                ?? throw new InvalidOperationException("PO not found");

            var sourceItems = db.Query<PoLineItem>(
                "SELECT PartID, ProductID, SKU, ItemName, QuantityOrdered, UnitCost FROM PurchaseOrderItems WHERE POID = @poid",
                new { poid = sourcePoid }, tx).ToList();

            // Insert draft PO with temp number
            var newPoid = db.QuerySingle<int>(@"
                INSERT INTO PurchaseOrders
                    (PONumber, SupplierID, Status, OrderDate, TotalCost, ShippingCost, Notes, CreatedBy, CreatedAt)
                VALUES
                    ('TEMP', @supplierId, 'Draft', GETDATE(), 0, @shippingCost, @notes, @createdBy, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    supplierId   = (int)source.SupplierID,
                    shippingCost = (decimal)source.ShippingCost,
                    notes        = (string?)source.Notes,
                    createdBy
                }, tx);

            // Use POID as PONumber — always unique
            db.Execute("UPDATE PurchaseOrders SET PONumber = @num WHERE POID = @poid",
                new { num = newPoid.ToString(), poid = newPoid }, tx);

            decimal total = 0;
            foreach (var item in sourceItems)
            {
                db.Execute(@"
                    INSERT INTO PurchaseOrderItems
                        (POID, PartID, ProductID, SKU, ItemName, QuantityOrdered, QuantityReceived, UnitCost)
                    VALUES (@poid, @partId, @productId, @sku, @itemName, @qty, 0, @cost)",
                    new
                    {
                        poid      = newPoid,
                        partId    = item.PartID,
                        productId = item.ProductID,
                        sku       = item.SKU,
                        itemName  = item.ItemName,
                        qty       = item.QuantityOrdered,
                        cost      = item.UnitCost
                    }, tx);
                total += item.QuantityOrdered * item.UnitCost;
            }

            db.Execute("UPDATE PurchaseOrders SET TotalCost = @total WHERE POID = @poid",
                new { total, poid = newPoid }, tx);

            tx.Commit();

            try
            {
                db.Execute(@"
                    INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
                    VALUES (@user, 'DuplicatePO', @details, GETDATE())",
                    new
                    {
                        user    = createdBy,
                        details = $"SourcePOID={sourcePoid} NewPOID={newPoid} Total={total:F2}"
                    });
            }
            catch (Exception auditEx) { _logger.LogError(auditEx, "[ApiPurchaseOrderRepository.DuplicatePO] Audit insert failed for NewPOID={Id}", newPoid); }

            return newPoid;
        }
        catch { tx.Rollback(); throw; }
    }

    public List<PoSummary> GetPosToReceive()
    {
        using var db = Connect();
        return db.Query<PoSummary>(@"
            SELECT  po.POID, po.PONumber, s.SupplierName,
                    po.ExpectedDate, po.Status,
                    SUM(poi.QuantityOrdered - poi.QuantityReceived) AS ItemsOutstanding
            FROM    PurchaseOrders po
            JOIN    Suppliers s            ON s.SupplierID = po.SupplierID
            JOIN    PurchaseOrderItems poi ON poi.POID      = po.POID
            WHERE   po.Status IN ('Sent', 'PartiallyReceived')
              AND   poi.QuantityReceived < poi.QuantityOrdered
            GROUP BY po.POID, po.PONumber, s.SupplierName, po.ExpectedDate, po.Status
            ORDER   BY po.ExpectedDate").ToList();
    }
}
