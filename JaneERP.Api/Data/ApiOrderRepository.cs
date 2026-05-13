using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiOrderRepository
{
    private readonly CompanyContext _ctx;
    public ApiOrderRepository(CompanyContext ctx) => _ctx = ctx;

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public (List<OrderListItem> Items, int Total) GetOrders(
        string? status, string? q, DateTime? from, DateTime? to, int page, int pageSize = 40)
    {
        using var db = Connect();
        var conditions = new List<string>();
        if (!string.IsNullOrEmpty(status)) conditions.Add("so.Status = @status");
        if (!string.IsNullOrEmpty(q))      conditions.Add("(c.FullName LIKE @q OR CAST(so.OrderNumber AS NVARCHAR) LIKE @q)");
        if (from.HasValue)                 conditions.Add("so.OrderDate >= @from");
        if (to.HasValue)                   conditions.Add("so.OrderDate <= @to");

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        var offset = (page - 1) * pageSize;
        var param  = new { status, q = string.IsNullOrEmpty(q) ? null : $"%{q}%", from, to, offset, pageSize };

        var total = db.ExecuteScalar<int>($@"
            SELECT COUNT(*) FROM SalesOrders so
            JOIN Customers c ON c.CustomerID = so.CustomerID
            {where}", param);

        var items = db.Query<OrderListItem>($@"
            SELECT  so.SalesOrderID, so.OrderNumber,
                    c.FullName    AS CustomerName,
                    c.Email       AS CustomerEmail,
                    so.OrderDate, so.TotalPrice, so.Currency,
                    so.Status, so.OrderType, so.IsPaid
            FROM    SalesOrders so
            JOIN    Customers c ON c.CustomerID = so.CustomerID
            {where}
            ORDER   BY so.OrderDate DESC
            OFFSET  @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            param).ToList();

        return (items, total);
    }

    public OrderDetail? GetOrderDetail(int salesOrderId)
    {
        using var db = Connect();
        var order = db.QueryFirstOrDefault<OrderDetail>(@"
            SELECT  so.SalesOrderID, so.OrderNumber,
                    c.FullName    AS CustomerName,
                    c.Email       AS CustomerEmail,
                    so.OrderDate, so.TotalPrice, so.Currency,
                    so.Status, so.OrderType, so.IsPaid, so.PaidAt,
                    so.Notes, ISNULL(so.ShippingCost, 0) AS ShippingCost
            FROM    SalesOrders so
            JOIN    Customers c ON c.CustomerID = so.CustomerID
            WHERE   so.SalesOrderID = @salesOrderId",
            new { salesOrderId });

        if (order == null) return null;

        order.Items = db.Query<OrderLineItem>(@"
            SELECT SalesOrderItemID, ProductID, SKU, Title, Quantity, UnitPrice
            FROM   SalesOrderItems
            WHERE  SalesOrderID = @salesOrderId
            ORDER  BY SalesOrderItemID",
            new { salesOrderId }).ToList();

        return order;
    }

    public int CreateManualOrder(CreateOrderRequest req, string createdBy)
    {
        using var db = new SqlConnection(_ctx.ConnectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            // Find or create customer
            var customerId = db.QueryFirstOrDefault<int?>(
                "SELECT CustomerID FROM Customers WHERE Email = @email",
                new { email = req.CustomerEmail }, tx);

            if (customerId == null)
            {
                customerId = db.QuerySingle<int>(@"
                    INSERT INTO Customers (Email, FullName)
                    VALUES (@email, @name);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { email = req.CustomerEmail, name = req.CustomerName }, tx);
            }

            // Next order number
            var orderNumber = db.ExecuteScalar<int>(
                "SELECT ISNULL(MAX(OrderNumber), 0) + 1 FROM SalesOrders", null, tx);

            // Calculate total
            var subtotal = req.Items.Sum(i => i.Quantity * i.UnitPrice);
            var total    = subtotal - req.DiscountAmount + req.ShippingCost;

            var salesOrderId = db.QuerySingle<int>(@"
                INSERT INTO SalesOrders
                    (OrderNumber, CustomerID, OrderDate, TotalPrice, Currency, Notes,
                     Status, OrderType, ShippingCost, IsPaid, InventoryAffected)
                VALUES
                    (@orderNumber, @customerId, @orderDate, @total, @currency, @notes,
                     'Draft', @orderType, @shippingCost, 0, 0);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    orderNumber,
                    customerId,
                    orderDate    = req.OrderDate,
                    total,
                    currency     = req.Currency,
                    notes        = req.Notes,
                    orderType    = req.OrderType,
                    shippingCost = req.ShippingCost
                }, tx);

            foreach (var item in req.Items)
            {
                db.Execute(@"
                    INSERT INTO SalesOrderItems (SalesOrderID, ProductID, SKU, Title, Quantity, UnitPrice)
                    VALUES (@salesOrderId, @productId, @sku, @title, @qty, @price);",
                    new
                    {
                        salesOrderId,
                        productId = item.ProductId,
                        sku       = item.Sku,
                        title     = item.Title,
                        qty       = item.Quantity,
                        price     = item.UnitPrice
                    }, tx);
            }

            tx.Commit();
            return salesOrderId;
        }
        catch { tx.Rollback(); throw; }
    }

    /// <summary>
    /// Updates order status, replicating the desktop app's inventory logic:
    /// - Complete: deducts inventory via InventoryTransactions (if not already done)
    /// - Draft (from Live/WIP): releases StockReservations
    /// </summary>
    public bool UpdateOrderStatus(int salesOrderId, string newStatus, string username)
    {
        using var db = new SqlConnection(_ctx.ConnectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var order = db.QueryFirstOrDefault(
                "SELECT SalesOrderID, Status, InventoryAffected FROM SalesOrders WHERE SalesOrderID = @id",
                new { id = salesOrderId }, tx);

            if (order == null) { tx.Rollback(); return false; }

            string currentStatus = (string)order.Status;
            bool   wasAffected   = false;
            try { wasAffected = (bool)order.InventoryAffected; } catch { }

            db.Execute("UPDATE SalesOrders SET Status = @s WHERE SalesOrderID = @id",
                new { s = newStatus, id = salesOrderId }, tx);

            // Deduct inventory when completing or shipping (mirrors desktop app behaviour)
            if ((newStatus == "Complete" || newStatus == "Shipped") && !wasAffected)
            {
                var items = db.Query<(int ProductID, string SKU, int Quantity)>(@"
                    SELECT ProductID, SKU, Quantity FROM SalesOrderItems WHERE SalesOrderID = @id",
                    new { id = salesOrderId }, tx).ToList();

                foreach (var item in items)
                {
                    db.Execute(@"
                        INSERT INTO InventoryTransactions
                            (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                        VALUES (@pid, @qty, 'Sale', @notes, GETDATE())",
                        new
                        {
                            pid   = item.ProductID,
                            qty   = -item.Quantity,
                            notes = $"Packed by {username} via mobile (SO #{salesOrderId})"
                        }, tx);
                }

                try
                {
                    db.Execute(
                        "UPDATE SalesOrders SET InventoryAffected = 1 WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);
                }
                catch { /* InventoryAffected column may not exist on older DBs */ }
            }

            // Release stock reservations when completing/shipping or reverting to Draft
            if (newStatus == "Complete" || newStatus == "Shipped" ||
                (newStatus == "Draft" && (currentStatus == "Live" || currentStatus == "WIP")))
            {
                try
                {
                    db.Execute("DELETE FROM StockReservations WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);
                }
                catch { /* StockReservations table may not exist */ }
            }

            tx.Commit();
            return true;
        }
        catch { tx.Rollback(); throw; }
    }

    /// <summary>
    /// Returns pick list: order items with their stock quantities and primary pick location.
    /// </summary>
    public List<PickListItem> GetPickList(int salesOrderId)
    {
        using var db = Connect();
        return db.Query<PickListItem>(@"
            SELECT  soi.SalesOrderItemID,
                    soi.ProductID,
                    soi.SKU,
                    soi.Title,
                    soi.Quantity AS QuantityNeeded,
                    ISNULL((
                        SELECT SUM(QuantityChange)
                        FROM   InventoryTransactions
                        WHERE  ProductID = soi.ProductID
                    ), 0) AS TotalStock,
                    (
                        SELECT TOP 1 ISNULL(l.LocationName, 'Unassigned')
                        FROM   InventoryTransactions it2
                        LEFT JOIN Locations l ON l.LocationID = it2.LocationID
                        WHERE  it2.ProductID = soi.ProductID
                        GROUP  BY l.LocationID, l.LocationName
                        HAVING SUM(it2.QuantityChange) > 0
                        ORDER  BY SUM(it2.QuantityChange) DESC
                    ) AS PrimaryLocation
            FROM    SalesOrderItems soi
            WHERE   soi.SalesOrderID = @salesOrderId
            ORDER   BY soi.SalesOrderItemID",
            new { salesOrderId }).ToList();
    }

    public decimal GetSalesTotal(int days)
    {
        using var db = Connect();
        return db.ExecuteScalar<decimal>(@"
            SELECT ISNULL(SUM(TotalPrice), 0)
            FROM   SalesOrders
            WHERE  OrderDate >= DATEADD(DAY, -@days, GETDATE())
              AND  Status NOT IN ('Draft')",
            new { days });
    }

    public bool UpdateNotes(int salesOrderId, string? notes)
    {
        using var db = Connect();
        return db.Execute(
            "UPDATE SalesOrders SET Notes = @notes WHERE SalesOrderID = @salesOrderId",
            new { notes, salesOrderId }) > 0;
    }

    public int GetOrdersToPackCount()
    {
        using var db = Connect();
        return db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM SalesOrders WHERE Status IN ('Live', 'WIP')");
    }

    public List<SoSummary> GetSosToPack()
    {
        using var db = Connect();
        return db.Query<SoSummary>(@"
            SELECT  so.SalesOrderID, so.OrderNumber,
                    c.FullName AS CustomerName,
                    so.TotalPrice, so.Status, so.OrderDate,
                    COUNT(soi.SalesOrderItemID) AS LineCount,
                    SUM(soi.Quantity)            AS TotalQty
            FROM    SalesOrders so
            JOIN    Customers c          ON c.CustomerID  = so.CustomerID
            JOIN    SalesOrderItems soi  ON soi.SalesOrderID = so.SalesOrderID
            WHERE   so.Status IN ('Live', 'WIP')
            GROUP BY so.SalesOrderID, so.OrderNumber, c.FullName,
                     so.TotalPrice, so.Status, so.OrderDate
            ORDER   BY so.OrderDate ASC").ToList();
    }
}
