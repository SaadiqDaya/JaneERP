using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiCustomerRepository
{
    private readonly CompanyContext _ctx;
    public ApiCustomerRepository(CompanyContext ctx) => _ctx = ctx;

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public (List<CustomerListItem> Items, int Total) Search(string? query, int page, int pageSize = 40)
    {
        using var db = Connect();
        var where  = string.IsNullOrWhiteSpace(query) ? "" : "WHERE c.FullName LIKE @q OR c.Email LIKE @q";
        var offset = (page - 1) * pageSize;

        var total = db.ExecuteScalar<int>($"SELECT COUNT(*) FROM Customers c {where}",
            new { q = $"%{query}%" });

        var items = db.Query<CustomerListItem>($@"
            SELECT  c.CustomerID, c.FullName, c.Email,
                    ISNULL((SELECT COUNT(*) FROM SalesOrders WHERE CustomerID = c.CustomerID), 0) AS OrderCount,
                    ISNULL((SELECT SUM(TotalPrice) FROM SalesOrders
                            WHERE CustomerID = c.CustomerID AND Status NOT IN ('Draft')), 0) AS TotalSpent
            FROM    Customers c
            {where}
            ORDER   BY c.FullName
            OFFSET  @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { q = $"%{query}%", offset, pageSize }).ToList();

        return (items, total);
    }

    public CustomerDetail? GetDetail(int customerId)
    {
        using var db = Connect();
        var customer = db.QueryFirstOrDefault<CustomerDetail>(@"
            SELECT  c.CustomerID, c.FullName, c.Email,
                    ISNULL((SELECT COUNT(*) FROM SalesOrders WHERE CustomerID = c.CustomerID), 0) AS OrderCount,
                    ISNULL((SELECT SUM(TotalPrice) FROM SalesOrders
                            WHERE CustomerID = c.CustomerID AND Status NOT IN ('Draft')), 0) AS TotalSpent
            FROM    Customers c
            WHERE   c.CustomerID = @customerId",
            new { customerId });

        if (customer == null) return null;

        customer.RecentOrders = db.Query<OrderListItem>(@"
            SELECT  so.SalesOrderID, so.OrderNumber,
                    c.FullName  AS CustomerName,
                    c.Email     AS CustomerEmail,
                    so.OrderDate, so.TotalPrice, so.Currency,
                    so.Status, so.OrderType, so.IsPaid
            FROM    SalesOrders so
            JOIN    Customers c ON c.CustomerID = so.CustomerID
            WHERE   so.CustomerID = @customerId
            ORDER   BY so.OrderDate DESC",
            new { customerId }).ToList();

        return customer;
    }
}
