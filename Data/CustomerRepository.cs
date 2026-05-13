using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    /// <summary>Customer read queries. EnsureSchema is handled by ShopifySyncService.</summary>
    public class CustomerRepository : ICustomerRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        public List<CustomerSummary> GetSummaries()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<CustomerSummary>(@"
                SELECT c.CustomerID,
                       ISNULL(c.FullName, '') AS FullName,
                       c.Email,
                       COUNT(so.SalesOrderID)        AS OrderCount,
                       ISNULL(SUM(so.TotalPrice), 0) AS TotalSpent
                FROM   Customers c
                LEFT JOIN SalesOrders so ON so.CustomerID = c.CustomerID
                GROUP BY c.CustomerID, c.FullName, c.Email
                ORDER  BY TotalSpent DESC").ToList();
        }

        public List<CustomerOrder> GetOrders(int customerId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<CustomerOrder>(@"
                SELECT SalesOrderID, OrderNumber, OrderDate, TotalPrice, Currency,
                       OrderType, Status, ISNULL(IsPaid, 0) AS IsPaid, PaidAt
                FROM   SalesOrders
                WHERE  CustomerID = @customerId
                ORDER  BY OrderDate DESC",
                new { customerId }).ToList();
        }

        public List<CustomerOrderItem> GetOrderLineItems(int salesOrderId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<CustomerOrderItem>(@"
                SELECT ISNULL(soi.SKU, '')                      AS SKU,
                       ISNULL(soi.Title, p.ProductName)         AS ProductName,
                       soi.Quantity,
                       soi.UnitPrice,
                       soi.Quantity * soi.UnitPrice             AS LineTotal
                FROM   SalesOrderItems soi
                LEFT JOIN Products p ON p.ProductID = soi.ProductID
                WHERE  soi.SalesOrderID = @salesOrderId
                ORDER  BY soi.SalesOrderItemID",
                new { salesOrderId }).ToList();
        }
    }
}
