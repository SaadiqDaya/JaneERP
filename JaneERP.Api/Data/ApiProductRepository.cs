using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiProductRepository
{
    private readonly CompanyContext _ctx;
    public ApiProductRepository(CompanyContext ctx) => _ctx = ctx;

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public (List<ProductSearchResult> Items, int Total) Search(string? query, int page, int pageSize = 40)
    {
        using var db = Connect();
        var where = string.IsNullOrWhiteSpace(query)
            ? "WHERE p.IsActive = 1"
            : "WHERE p.IsActive = 1 AND (p.SKU LIKE @q OR p.ProductName LIKE @q)";
        var param = new { q = $"%{query}%" };
        var offset = (page - 1) * pageSize;

        var total = db.ExecuteScalar<int>($"SELECT COUNT(*) FROM Products p {where}", param);
        var items = db.Query<ProductSearchResult>($@"
            SELECT  p.ProductID,
                    p.SKU,
                    p.ProductName,
                    ISNULL((SELECT SUM(QuantityChange) FROM InventoryTransactions WHERE ProductID = p.ProductID), 0) AS CurrentStock,
                    p.RetailPrice,
                    p.ReorderPoint,
                    CASE WHEN ISNULL((SELECT SUM(QuantityChange) FROM InventoryTransactions WHERE ProductID = p.ProductID), 0) <= p.ReorderPoint
                         AND p.ReorderPoint > 0 THEN 1 ELSE 0 END AS IsLowStock
            FROM    Products p
            {where}
            ORDER   BY p.ProductName
            OFFSET  @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { q = $"%{query}%", offset, pageSize }).ToList();

        return (items, total);
    }

    public List<StockByLocation> GetStockByLocation(int productId)
    {
        using var db = Connect();
        return db.Query<StockByLocation>(@"
            SELECT  ISNULL(l.LocationID, 0)     AS LocationID,
                    ISNULL(l.LocationName, 'No Location') AS LocationName,
                    SUM(t.QuantityChange)        AS Stock
            FROM    InventoryTransactions t
            LEFT JOIN Locations l ON l.LocationID = t.LocationID
            WHERE   t.ProductID = @productId
            GROUP BY l.LocationID, l.LocationName
            HAVING  SUM(t.QuantityChange) <> 0
            ORDER   BY l.LocationName",
            new { productId }).ToList();
    }

    public int GetLowStockCount()
    {
        using var db = Connect();
        return db.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM Products p
            WHERE  p.IsActive = 1
              AND  p.ReorderPoint > 0
              AND  ISNULL((SELECT SUM(QuantityChange) FROM InventoryTransactions WHERE ProductID = p.ProductID), 0) <= p.ReorderPoint");
    }

    public int GetTotalActiveProducts()
    {
        using var db = Connect();
        return db.ExecuteScalar<int>("SELECT COUNT(*) FROM Products WHERE IsActive = 1");
    }

    public (List<ProductSearchResult> Items, int Total) GetLowStock(int page, int pageSize = 40)
    {
        using var db = Connect();
        var offset = (page - 1) * pageSize;

        var total = db.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM Products p
            WHERE  p.IsActive = 1 AND p.ReorderPoint > 0
              AND  ISNULL((SELECT SUM(QuantityChange) FROM InventoryTransactions WHERE ProductID = p.ProductID), 0) <= p.ReorderPoint");

        var items = db.Query<ProductSearchResult>(@"
            SELECT  p.ProductID, p.SKU, p.ProductName,
                    ISNULL((SELECT SUM(QuantityChange) FROM InventoryTransactions WHERE ProductID = p.ProductID), 0) AS CurrentStock,
                    p.RetailPrice, p.ReorderPoint, 1 AS IsLowStock
            FROM    Products p
            WHERE   p.IsActive = 1 AND p.ReorderPoint > 0
              AND   ISNULL((SELECT SUM(QuantityChange) FROM InventoryTransactions WHERE ProductID = p.ProductID), 0) <= p.ReorderPoint
            ORDER   BY p.ProductName
            OFFSET  @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { offset, pageSize }).ToList();

        return (items, total);
    }

    public void AdjustStock(int productId, int qty, string reason, string username)
    {
        using var db = Connect();
        db.Execute(@"
            INSERT INTO InventoryTransactions
                (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
            VALUES
                (@productId, @qty, 'Adjustment', @notes, GETDATE())",
            new { productId, qty, notes = $"Manual adjustment by {username}: {reason}" });
    }

    public (List<StockTransaction> Items, int Total, string ProductName, string SKU) GetTransactionHistory(
        int productId, int page, int pageSize = 30)
    {
        using var db = Connect();
        var offset = (page - 1) * pageSize;

        var total = db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM InventoryTransactions WHERE ProductID = @productId",
            new { productId });

        var items = db.Query<StockTransaction>(@"
            SELECT  t.TransactionID, t.QuantityChange, t.TransactionType,
                    t.Notes, t.TransactionDate,
                    ISNULL(l.LocationName, '') AS LocationName
            FROM    InventoryTransactions t
            LEFT JOIN Locations l ON l.LocationID = t.LocationID
            WHERE   t.ProductID = @productId
            ORDER   BY t.TransactionDate DESC
            OFFSET  @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { productId, offset, pageSize }).ToList();

        var prod = db.QueryFirstOrDefault(
            "SELECT ProductName, SKU FROM Products WHERE ProductID = @productId",
            new { productId });

        return (items, total, (string?)prod?.ProductName ?? "", (string?)prod?.SKU ?? "");
    }
}
