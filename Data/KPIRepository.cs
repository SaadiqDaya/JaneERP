using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class KPIRepository : IKPIRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        public KpiSummary GetKPIs()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var kpi = new KpiSummary();

            kpi.OrdersToday = Safe<int>(db,
                "SELECT COUNT(*) FROM SalesOrders WHERE CAST(OrderDate AS DATE) = CAST(GETDATE() AS DATE)");

            kpi.RevenueToday = Safe<decimal>(db,
                "SELECT ISNULL(SUM(TotalPrice),0) FROM SalesOrders WHERE CAST(OrderDate AS DATE) = CAST(GETDATE() AS DATE)");

            kpi.PendingOrders = Safe<int>(db,
                "SELECT COUNT(*) FROM SalesOrders WHERE Status IN ('Draft','Live')");

            // Stock KPIs use a single derived-table JOIN to avoid N+1 correlated subqueries at scale
            const string stockSql = @"
                SELECT
                    SUM(CASE WHEN ISNULL(inv.Qty,0) >  0 THEN 1 ELSE 0 END) AS InStock,
                    SUM(CASE WHEN ISNULL(inv.Qty,0) <= 0 THEN 1 ELSE 0 END) AS OutOfStock,
                    SUM(CASE WHEN p.ReorderPoint > 0
                              AND ISNULL(inv.Qty,0) > 0
                              AND ISNULL(inv.Qty,0) <= p.ReorderPoint THEN 1 ELSE 0 END) AS LowStock
                FROM Products p
                LEFT JOIN (
                    SELECT ProductID, SUM(QuantityChange) AS Qty
                    FROM   InventoryTransactions
                    GROUP  BY ProductID
                ) inv ON inv.ProductID = p.ProductID
                WHERE p.IsActive = 1";

            try
            {
                var row = db.QueryFirstOrDefault(stockSql);
                if (row != null)
                {
                    kpi.InStock    = (int)(row.InStock    ?? 0);
                    kpi.OutOfStock = (int)(row.OutOfStock ?? 0);
                    kpi.LowStock   = (int)(row.LowStock   ?? 0);
                }
            }
            catch (Exception ex) { Logging.AppLogger.Info($"[KPIRepository.GetKPIs] Stock KPI query failed (table may not exist yet): {ex.Message}"); }

            kpi.OpenWorkOrders = Safe<int>(db,
                "SELECT COUNT(*) FROM WorkOrders WHERE Status <> 'Complete'");

            kpi.TasksOverdue = Safe<int>(db,
                "SELECT COUNT(*) FROM Tasks WHERE Status <> 'Done' AND DueDate < GETDATE()");

            kpi.InventoryValue = Safe<decimal>(db, @"
                SELECT ISNULL(SUM(ISNULL(inv.Qty,0) * p.WholesalePrice), 0)
                FROM   Products p
                LEFT JOIN (
                    SELECT ProductID, SUM(QuantityChange) AS Qty
                    FROM   InventoryTransactions
                    GROUP  BY ProductID
                ) inv ON inv.ProductID = p.ProductID
                WHERE  p.IsActive = 1");

            kpi.PendingPOs = Safe<int>(db,
                "SELECT COUNT(*) FROM PurchaseOrders WHERE Status NOT IN ('Received','Cancelled')");

            kpi.OutstandingPOAmount = Safe<decimal>(db,
                "SELECT ISNULL(SUM(ISNULL(TotalCost,0)+ISNULL(ShippingCost,0)+ISNULL(TaxAmount,0)),0) FROM PurchaseOrders WHERE Status NOT IN ('Received','Cancelled')");

            kpi.TasksOpenTotal = Safe<int>(db,
                "SELECT COUNT(*) FROM Tasks WHERE Status != 'Done'");

            kpi.TasksDueThisWeek = Safe<int>(db,
                "SELECT COUNT(*) FROM Tasks WHERE Status != 'Done' AND DueDate >= GETDATE() AND DueDate <= DATEADD(day,7,GETDATE())");

            kpi.ActiveUsers = Safe<int>(db,
                "SELECT COUNT(*) FROM Users WHERE IsActive = 1");

            kpi.TotalProducts = Safe<int>(db,
                "SELECT COUNT(*) FROM Products WHERE IsActive = 1");

            kpi.TotalParts = Safe<int>(db,
                "SELECT COUNT(*) FROM Parts WHERE IsActive = 1");

            kpi.ExpensesLast7Days = Safe<decimal>(db,
                "SELECT ISNULL(SUM(Amount),0) FROM Expenses WHERE ExpenseDate >= DATEADD(day,-7,GETDATE())");

            kpi.ExpensesLast30Days = Safe<decimal>(db,
                "SELECT ISNULL(SUM(Amount),0) FROM Expenses WHERE ExpenseDate >= DATEADD(day,-30,GETDATE())");

            kpi.RevenueLast7Days = Safe<decimal>(db,
                "SELECT ISNULL(SUM(TotalPrice),0) FROM SalesOrders WHERE OrderDate >= DATEADD(day,-7,GETDATE())");

            kpi.RevenueLast30Days = Safe<decimal>(db,
                "SELECT ISNULL(SUM(TotalPrice),0) FROM SalesOrders WHERE OrderDate >= DATEADD(day,-30,GETDATE())");

            kpi.OrdersLast7Days = Safe<int>(db,
                "SELECT COUNT(*) FROM SalesOrders WHERE OrderDate >= DATEADD(day,-7,GETDATE())");

            kpi.OrdersLast30Days = Safe<int>(db,
                "SELECT COUNT(*) FROM SalesOrders WHERE OrderDate >= DATEADD(day,-30,GETDATE())");

            return kpi;
        }

        private static T Safe<T>(IDbConnection db, string sql, object? param = null)
        {
            try { return db.ExecuteScalar<T>(sql, param) ?? default!; }
            catch (Exception ex) { Logging.AppLogger.Info($"[KPIRepository.Safe] Query failed: {ex.Message}"); return default!; }
        }
    }
}
