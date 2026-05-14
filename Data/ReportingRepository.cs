using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    /// <summary>
    /// Read-only SQL queries backing the Reports screen.
    /// Extracted from FormReports so any app or API can call the same queries.
    /// </summary>
    public class ReportingRepository : IReportingRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        public IEnumerable<dynamic> GetStockOnHand()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query(@"
                SELECT l.LocationName, p.SKU, p.ProductName,
                       SUM(t.QuantityChange)                         AS StockQty,
                       SUM(t.QuantityChange) * p.RetailPrice         AS RetailValue,
                       SUM(t.QuantityChange) * p.WholesalePrice      AS WholesaleValue
                FROM   InventoryTransactions t
                JOIN   Products   p ON p.ProductID   = t.ProductID
                LEFT JOIN Locations l ON l.LocationID = t.LocationID
                WHERE  p.IsActive = 1
                GROUP  BY l.LocationName, p.SKU, p.ProductName, p.RetailPrice, p.WholesalePrice
                HAVING SUM(t.QuantityChange) > 0
                ORDER  BY l.LocationName, p.SKU").ToList();
        }

        public IEnumerable<dynamic> GetSalesByPeriod(DateTime from, DateTime to)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query(@"
                SELECT so.OrderNumber AS [Order#],
                       so.OrderDate  AS [Date],
                       c.FullName    AS Customer,
                       ISNULL(st.StoreName, so.OrderType) AS Store,
                       so.TotalPrice AS Total,
                       so.Currency,
                       so.Status,
                       so.OrderType  AS [Type]
                FROM   SalesOrders so
                JOIN   Customers c    ON c.CustomerID = so.CustomerID
                LEFT JOIN Stores st   ON st.StoreID   = so.StoreID
                WHERE  so.OrderDate >= @from AND so.OrderDate < @to
                ORDER  BY so.OrderDate DESC",
                new { from, to }).ToList();
        }

        public IEnumerable<dynamic> GetCogsSummary()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query(@"
                SELECT wo.WorkOrderID, p.SKU, p.ProductName,
                       wo.Quantity,
                       ISNULL(wo.CostOfGoods, 0) AS CostOfGoods,
                       wo.CompletedAt
                FROM   WorkOrders wo
                JOIN   Products p ON p.ProductID = wo.ProductID
                WHERE  wo.Status = 'Complete' AND wo.CostOfGoods IS NOT NULL
                  AND  p.IsActive = 1
                ORDER  BY wo.CompletedAt DESC").ToList();
        }

        public IEnumerable<dynamic> GetCycleCountVariance(DateTime from, DateTime to)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query(@"
                SELECT p.SKU,
                       p.ProductName,
                       l.LocationName,
                       it.QuantityChange AS AdjustmentQty,
                       p.LastVerifiedAt  AS DateCounted,
                       p.LastVerifiedBy  AS VerifiedBy,
                       ISNULL((SELECT SUM(t2.QuantityChange)
                               FROM   InventoryTransactions t2
                               WHERE  t2.ProductID = p.ProductID
                                 AND  t2.TransactionID <= it.TransactionID), 0)
                           - it.QuantityChange AS ExpectedQty,
                       ISNULL((SELECT SUM(t2.QuantityChange)
                               FROM   InventoryTransactions t2
                               WHERE  t2.ProductID = p.ProductID
                                 AND  t2.TransactionID <= it.TransactionID), 0) AS CountedQty
                FROM   Products p
                JOIN   InventoryTransactions it
                           ON  it.ProductID      = p.ProductID
                           AND it.TransactionType = 'Cycle Count'
                           AND it.TransactionDate >= @from
                           AND it.TransactionDate <  @to
                LEFT JOIN Locations l ON l.LocationID = it.LocationID
                WHERE  p.IsActive = 1
                  AND  p.LastVerifiedAt IS NOT NULL
                ORDER  BY it.TransactionDate DESC, p.ProductName",
                new { from, to }).ToList();
        }

        public IEnumerable<dynamic> GetGrossProfitByProduct(DateTime from, DateTime to)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query(@"
                WITH ProductUnitCost AS (
                    SELECT  ProductID,
                            SUM(CostOfGoods) / NULLIF(SUM(Quantity), 0) AS UnitCOGS
                    FROM    WorkOrders
                    WHERE   Status      = 'Complete'
                      AND   CostOfGoods IS NOT NULL
                    GROUP BY ProductID
                )
                SELECT
                    p.SKU,
                    p.ProductName                                          AS Product,
                    SUM(soi.Quantity)                                      AS UnitsSold,
                    SUM(soi.Quantity * soi.UnitPrice)                      AS Revenue,
                    SUM(soi.Quantity * ISNULL(pc.UnitCOGS, 0))            AS COGS,
                    SUM(soi.Quantity * soi.UnitPrice)
                    - SUM(soi.Quantity * ISNULL(pc.UnitCOGS, 0))          AS GrossProfit
                FROM   SalesOrderItems  soi
                JOIN   Products         p  ON p.ProductID     = soi.ProductID
                JOIN   SalesOrders      so ON so.SalesOrderID = soi.SalesOrderID
                LEFT JOIN ProductUnitCost pc ON pc.ProductID  = soi.ProductID
                WHERE  so.OrderDate >= @from AND so.OrderDate < @to
                  AND  p.IsActive = 1
                GROUP  BY p.SKU, p.ProductName
                ORDER  BY GrossProfit DESC",
                new { from, to }).ToList();
        }

        public int GetSalesOrderItemCount()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.ExecuteScalar<int>("SELECT COUNT(1) FROM SalesOrderItems");
        }
    }
}
