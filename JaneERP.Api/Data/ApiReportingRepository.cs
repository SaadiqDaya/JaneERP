using System.Data;
using Dapper;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiReportingRepository
{
    private readonly CompanyContext _ctx;
    public ApiReportingRepository(CompanyContext ctx) => _ctx = ctx;

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    /// <summary>Current stock for all active products across all locations.</summary>
    public List<StockSnapshotRow> GetStockSnapshot()
    {
        using var db = Connect();
        return db.Query<StockSnapshotRow>(@"
            SELECT  p.ProductID, p.SKU, p.ProductName,
                    ISNULL(inv.CurrentStock, 0) AS CurrentStock,
                    p.ReorderPoint,
                    CASE WHEN ISNULL(inv.CurrentStock, 0) <= p.ReorderPoint AND p.ReorderPoint > 0
                         THEN 1 ELSE 0 END AS IsLowStock
            FROM    Products p
            LEFT JOIN (
                SELECT ProductID, SUM(QuantityChange) AS CurrentStock
                FROM   InventoryTransactions
                GROUP  BY ProductID
            ) inv ON inv.ProductID = p.ProductID
            WHERE   p.IsActive = 1
            ORDER   BY p.SKU").ToList();
    }

    /// <summary>Sales summary rows for a date range — one row per order.</summary>
    public List<SalesReportRow> GetSalesReport(DateTime from, DateTime to)
    {
        using var db = Connect();
        return db.Query<SalesReportRow>(@"
            SELECT  so.SalesOrderID,
                    so.OrderDate,
                    ISNULL(c.FirstName + ' ' + c.LastName, 'Unknown') AS CustomerName,
                    so.Status,
                    so.TotalPrice,
                    so.IsPaid
            FROM    SalesOrders so
            LEFT JOIN Customers c ON c.CustomerID = so.CustomerID
            WHERE   so.OrderDate >= @from AND so.OrderDate <= @to
            ORDER   BY so.OrderDate DESC", new { from, to }).ToList();
    }

    /// <summary>COGS report — completed work orders with cost data in a date range.</summary>
    public List<CogsReportRow> GetCogsReport(DateTime from, DateTime to)
    {
        using var db = Connect();
        return db.Query<CogsReportRow>(@"
            SELECT  wo.WorkOrderID,
                    p.SKU,
                    p.ProductName,
                    wo.Quantity,
                    ISNULL(wo.CostOfGoods, 0) AS CostOfGoods,
                    wo.CompletedAt
            FROM    WorkOrders wo
            JOIN    Products p ON p.ProductID = wo.ProductID
            WHERE   wo.Status = 'Complete'
              AND   wo.CompletedAt >= @from AND wo.CompletedAt <= @to
              AND   wo.CostOfGoods IS NOT NULL
            ORDER   BY wo.CompletedAt DESC", new { from, to }).ToList();
    }

    /// <summary>Gross-profit summary per product — joining sales lines with COGS.</summary>
    public GrossProfitSummary GetGrossProfitSummary(DateTime from, DateTime to)
    {
        using var db = Connect();

        decimal revenue = db.ExecuteScalar<decimal>(@"
            SELECT ISNULL(SUM(TotalPrice), 0)
            FROM   SalesOrders
            WHERE  OrderDate >= @from AND OrderDate <= @to
              AND  (Status = 'Complete' OR IsPaid = 1)", new { from, to });

        decimal cogs = db.ExecuteScalar<decimal>(@"
            SELECT ISNULL(SUM(CostOfGoods), 0)
            FROM   WorkOrders
            WHERE  Status = 'Complete'
              AND  CompletedAt >= @from AND CompletedAt <= @to
              AND  CostOfGoods IS NOT NULL", new { from, to });

        return new GrossProfitSummary(revenue, cogs, revenue - cogs);
    }
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record StockSnapshotRow(
    int    ProductID,
    string SKU,
    string ProductName,
    int    CurrentStock,
    int    ReorderPoint,
    bool   IsLowStock)
{
    public StockSnapshotRow() : this(0, "", "", 0, 0, false) { }
}

public record SalesReportRow(
    int      SalesOrderID,
    DateTime OrderDate,
    string   CustomerName,
    string   Status,
    decimal  TotalPrice,
    bool     IsPaid)
{
    public SalesReportRow() : this(0, default, "", "", 0, false) { }
}

public record CogsReportRow(
    int       WorkOrderID,
    string    SKU,
    string    ProductName,
    int       Quantity,
    decimal   CostOfGoods,
    DateTime? CompletedAt)
{
    public CogsReportRow() : this(0, "", "", 0, 0, null) { }
}

public record GrossProfitSummary(decimal Revenue, decimal Cogs, decimal GrossProfit);
