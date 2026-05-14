using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiWorkOrderRepository
{
    private readonly CompanyContext                    _ctx;
    private readonly ILogger<ApiWorkOrderRepository> _logger;

    public ApiWorkOrderRepository(CompanyContext ctx, ILogger<ApiWorkOrderRepository> logger)
    {
        _ctx    = ctx;
        _logger = logger;
    }

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
        catch (Exception ex) { _logger.LogError(ex, "[ApiWorkOrderRepository.GetWorkOrders] Query failed"); return []; }
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
        catch (Exception ex) { _logger.LogError(ex, "[ApiWorkOrderRepository.GetDetail] Query failed for WorkOrderID={Id}", workOrderId); return null; }
    }

    // Roles: admin, warehouse
    public bool UpdateStatus(int workOrderId, string newStatus, string? username)
    {
        using var db = new SqlConnection(_ctx.ConnectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var exists = db.QueryFirstOrDefault<int?>(
                "SELECT WorkOrderID FROM WorkOrders WHERE WorkOrderID = @workOrderId",
                new { workOrderId }, tx);
            if (exists == null) { tx.Rollback(); return false; }

            var extraSets = newStatus is "Complete" or "Completed"
                ? ", CompletedAt = ISNULL(CompletedAt, GETDATE())"
                : "";

            db.Execute(
                $"UPDATE WorkOrders SET Status = @newStatus{extraSets} WHERE WorkOrderID = @workOrderId",
                new { newStatus, workOrderId }, tx);

            tx.Commit();

            try
            {
                db.Execute(@"
                    INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
                    VALUES (@user, 'UpdateWorkOrderStatus', @details, GETDATE())",
                    new
                    {
                        user    = username ?? "system",
                        details = $"WorkOrderID={workOrderId} NewStatus={newStatus}"
                    });
            }
            catch (Exception auditEx) { _logger.LogError(auditEx, "[ApiWorkOrderRepository.UpdateStatus] Audit insert failed for WorkOrderID={Id}", workOrderId); }

            return true;
        }
        catch { tx.Rollback(); throw; }
    }
}
