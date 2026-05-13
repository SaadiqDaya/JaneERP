using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class ExportRepository : IExportRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        private IDbConnection Open() => new SqlConnection(_connectionString);

        public IEnumerable<dynamic> GetProductsForExport()
        {
            using var db = Open();
            return db.Query(@"
                SELECT  p.SKU,
                        p.ProductName,
                        p.UnitOfMeasure,
                        p.RetailPrice,
                        p.WholesalePrice,
                        p.ReorderPoint,
                        ISNULL(p.OrderUpTo, 0) AS OrderUpTo,
                        ISNULL((SELECT SUM(t.QuantityChange) FROM InventoryTransactions t WHERE t.ProductID = p.ProductID), 0) AS CurrentStock,
                        pt.TypeName    AS [Type],
                        l.LocationName AS Location
                FROM    Products p
                LEFT JOIN ProductTypes pt ON pt.ProductTypeID = p.ProductTypeID
                LEFT JOIN Locations    l  ON l.LocationID     = p.DefaultLocationID
                WHERE   p.IsActive = 1
                ORDER BY p.SKU").AsList();
        }

        public IEnumerable<dynamic> GetInventoryByLocationForExport()
        {
            using var db = Open();
            return db.Query(@"
                SELECT  p.SKU,
                        p.ProductName,
                        l.LocationName,
                        SUM(t.QuantityChange) AS StockQty
                FROM    InventoryTransactions t
                JOIN    Products  p ON p.ProductID  = t.ProductID
                JOIN    Locations l ON l.LocationID = t.LocationID
                WHERE   p.IsActive = 1
                GROUP BY p.SKU, p.ProductName, l.LocationName
                ORDER BY p.SKU, l.LocationName").AsList();
        }

        public IEnumerable<dynamic> GetReorderSummaryForExport()
        {
            using var db = Open();
            return db.Query(@"
                SELECT  p.SKU,
                        p.ProductName,
                        ISNULL((SELECT SUM(t.QuantityChange) FROM InventoryTransactions t WHERE t.ProductID = p.ProductID), 0) AS CurrentStock,
                        p.ReorderPoint,
                        p.ReorderPoint - ISNULL((SELECT SUM(t.QuantityChange) FROM InventoryTransactions t WHERE t.ProductID = p.ProductID), 0) AS Shortfall
                FROM    Products p
                WHERE   p.IsActive = 1
                  AND   ISNULL((SELECT SUM(t.QuantityChange) FROM InventoryTransactions t WHERE t.ProductID = p.ProductID), 0) <= p.ReorderPoint
                ORDER BY Shortfall DESC").AsList();
        }

        public IEnumerable<dynamic> GetPartsForExport()
        {
            using var db = Open();
            return db.Query(@"
                SELECT  PartNumber,
                        PartName,
                        UnitOfMeasure,
                        CurrentStock,
                        UnitCost,
                        ISNULL(ReorderPoint, 0) AS ReorderPoint
                FROM    Parts
                WHERE   IsActive = 1
                ORDER BY PartNumber").AsList();
        }

        public IEnumerable<dynamic> GetSalesOrdersForExport(DateTime from, DateTime to)
        {
            using var db = Open();
            return db.Query(@"
                SELECT  so.OrderNumber,
                        c.FullName     AS CustomerName,
                        so.OrderDate,
                        so.Status,
                        so.TotalPrice  AS Total,
                        ISNULL(so.DiscountAmount, 0) AS Discount
                FROM    SalesOrders so
                LEFT JOIN Customers c ON c.CustomerID = so.CustomerID
                WHERE   so.OrderDate >= @from AND so.OrderDate <= @to
                ORDER BY so.OrderDate DESC", new { from, to }).AsList();
        }

        public IEnumerable<dynamic> GetSalesOrderLineItemsForExport(DateTime from, DateTime to)
        {
            using var db = Open();
            return db.Query(@"
                SELECT  so.OrderNumber,
                        so.OrderDate,
                        so.Status,
                        ISNULL(soi.SKU, '')                          AS SKU,
                        ISNULL(soi.Title, p.ProductName)             AS ProductName,
                        soi.Quantity,
                        soi.UnitPrice,
                        soi.Quantity * soi.UnitPrice                 AS LineTotal,
                        ISNULL(c.FullName, '')                       AS Customer
                FROM    SalesOrderItems soi
                JOIN    SalesOrders so ON so.SalesOrderID = soi.SalesOrderID
                LEFT JOIN Products   p  ON p.ProductID   = soi.ProductID
                LEFT JOIN Customers  c  ON c.CustomerID  = so.CustomerID
                WHERE   so.OrderDate >= @from AND so.OrderDate <= @to
                ORDER   BY so.OrderDate DESC, so.OrderNumber, soi.SalesOrderItemID",
                new { from, to }).AsList();
        }

        public IEnumerable<dynamic> GetPurchaseOrdersForExport(DateTime from, DateTime to)
        {
            using var db = Open();
            return db.Query(@"
                SELECT  po.PONumber,
                        s.SupplierName,
                        po.Status,
                        po.OrderDate,
                        po.TotalCost
                FROM    PurchaseOrders po
                LEFT JOIN Suppliers s ON s.SupplierID = po.SupplierID
                WHERE   po.OrderDate >= @from AND po.OrderDate <= @to
                ORDER BY po.OrderDate DESC", new { from, to }).AsList();
        }

        public IEnumerable<dynamic> GetWorkOrdersForExport()
        {
            using var db = Open();
            return db.Query(@"
                SELECT  wo.WorkOrderID,
                        mo.MONumber,
                        p.ProductName,
                        wo.Quantity,
                        wo.Status,
                        ISNULL(wo.CostOfGoods, 0) AS COGS,
                        wo.CompletedAt
                FROM    WorkOrders wo
                LEFT JOIN ManufacturingOrders mo ON mo.MOID     = wo.MOID
                LEFT JOIN Products            p  ON p.ProductID = wo.ProductID
                ORDER BY wo.WorkOrderID DESC").AsList();
        }

        public IEnumerable<dynamic> GetCogsSummaryForExport()
        {
            using var db = Open();
            return db.Query(@"
                SELECT  wo.WorkOrderID,
                        mo.MONumber,
                        p.ProductName,
                        wo.Quantity,
                        ISNULL(wo.CostOfGoods, 0)                         AS TotalCOGS,
                        CASE WHEN wo.Quantity > 0
                             THEN ISNULL(wo.CostOfGoods, 0) / wo.Quantity
                             ELSE 0 END                                    AS COGSPerUnit,
                        wo.CompletedAt
                FROM    WorkOrders wo
                LEFT JOIN ManufacturingOrders mo ON mo.MOID     = wo.MOID
                LEFT JOIN Products            p  ON p.ProductID = wo.ProductID
                WHERE   wo.Status = 'Complete'
                ORDER BY wo.CompletedAt DESC").AsList();
        }

        public IEnumerable<dynamic> GetCustomerListForExport()
        {
            using var db = Open();
            return db.Query(@"
                SELECT  ISNULL(c.FullName, '') AS Name,
                        ISNULL(c.Email,    '') AS Email,
                        COUNT(so.SalesOrderID)        AS OrderCount,
                        ISNULL(SUM(so.TotalPrice), 0) AS TotalSpent
                FROM    Customers c
                LEFT JOIN SalesOrders so ON so.CustomerID = c.CustomerID
                GROUP BY c.CustomerID, c.FullName, c.Email
                ORDER BY c.FullName").AsList();
        }
    }
}
