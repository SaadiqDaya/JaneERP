using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiWorkOrderRepository
{
    private readonly CompanyContext _ctx;
    public ApiWorkOrderRepository(CompanyContext ctx) => _ctx = ctx;

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public List<WorkOrderItem> GetWorkOrders(string? status)
    {
        using var db = Connect();
        var where = status switch
        {
            "open"     => "WHERE wo.Status NOT IN ('Complete','Completed','Cancelled')",
            "progress" => "WHERE wo.Status = 'InProgress'",
            "done"     => "WHERE wo.Status IN ('Complete','Completed')",
            _           => ""
        };

        // ManufacturingOrders / WorkOrders tables may not exist on all company DBs
        try
        {
            return db.Query<WorkOrderItem>($@"
                SELECT  wo.WorkOrderID, wo.MOID,
                        mo.MONumber,
                        p.ProductName, p.SKU,
                        wo.Quantity,
                        ISNULL(wo.CompletedQty, 0) AS CompletedQty,
                        wo.Status,
                        wo.AssignedTo,
                        wo.CompletedAt,
                        wo.Notes
                FROM    WorkOrders wo
                JOIN    ManufacturingOrders mo ON mo.MOID    = wo.MOID
                JOIN    Products            p  ON p.ProductID = wo.ProductID
                {where}
                ORDER   BY wo.WorkOrderID DESC",
                new { status }).ToList();
        }
        catch { return []; }
    }

    public WorkOrderItem? GetDetail(int workOrderId)
    {
        using var db = Connect();
        try
        {
            return db.QueryFirstOrDefault<WorkOrderItem>(@"
                SELECT  wo.WorkOrderID, wo.MOID,
                        mo.MONumber,
                        p.ProductName, p.SKU,
                        wo.Quantity,
                        ISNULL(wo.CompletedQty, 0) AS CompletedQty,
                        wo.Status,
                        wo.AssignedTo,
                        wo.CompletedAt,
                        wo.Notes
                FROM    WorkOrders wo
                JOIN    ManufacturingOrders mo ON mo.MOID    = wo.MOID
                JOIN    Products            p  ON p.ProductID = wo.ProductID
                WHERE   wo.WorkOrderID = @workOrderId",
                new { workOrderId });
        }
        catch { return null; }
    }
}
