using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using JaneERP.Core.Models;
using JaneERP.Core.Services;
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
            "progress" => "WHERE wo.Status IN ('Live','InProgress')",
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

    // ── Lot availability / Go-Live / Complete ─────────────────────────────────

    public List<PartLotAvailability> GetLotAvailability(int workOrderId)
    {
        var svc = new WorkOrderService(_ctx.ConnectionString);
        return svc.GetLotAvailability(workOrderId);
    }

    public void GoLive(int workOrderId, IEnumerable<LotReservation> reservations, string username)
    {
        var svc = new WorkOrderService(_ctx.ConnectionString);
        svc.GoLive(workOrderId, reservations, username);

        try
        {
            using var db = Connect();
            db.Execute(@"
                INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
                VALUES (@user, 'WorkOrderGoLive', @details, GETDATE())",
                new { user = username, details = $"WorkOrderID={workOrderId}" });
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiWorkOrderRepository.GoLive] Audit failed for WO {Id}", workOrderId); }
    }

    public void CompleteWorkOrder(int workOrderId, CompleteWorkOrderRequest req, string username)
    {
        var svc = new WorkOrderService(_ctx.ConnectionString);
        svc.Complete(workOrderId, req, username);

        try
        {
            using var db = Connect();
            db.Execute(@"
                INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
                VALUES (@user, 'WorkOrderComplete', @details, GETDATE())",
                new { user = username, details = $"WorkOrderID={workOrderId} CompletedQty={req.CompletedQty} ScrapQty={req.ScrapQty}" });
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiWorkOrderRepository.CompleteWorkOrder] Audit failed for WO {Id}", workOrderId); }
    }

    public List<Models.WOBomPreviewRow> GetBomPreview(int workOrderId)
    {
        var svc = new WorkOrderService(_ctx.ConnectionString);
        return svc.GetBomPreview(workOrderId).Select(r => new Models.WOBomPreviewRow
        {
            PartID           = r.PartID,
            PartNumber       = r.PartNumber,
            PartName         = r.PartName,
            UOM              = r.UOM,
            RequiredQty      = r.RequiredQty,
            OnHand           = r.OnHand,
            CreatesBatchLoss = r.CreatesBatchLoss,
            BatchLossRate    = r.BatchLossRate,
        }).ToList();
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

            // Stock check: verify all BOM ingredients have enough stock before starting
            if (newStatus == "InProgress")
            {
                var wo = db.QueryFirstOrDefault(
                    "SELECT ProductID, Quantity FROM WorkOrders WHERE WorkOrderID = @workOrderId",
                    new { workOrderId }, tx);
                if (wo != null)
                {
                    var shortItems = db.Query(@"
                        SELECT pt.PartName,
                               pp.Quantity * @woQty AS Required,
                               pt.CurrentStock      AS OnHand
                        FROM   ProductParts pp
                        JOIN   Parts        pt ON pt.PartID = pp.PartID
                        WHERE  pp.ProductID = @productId
                        AND    pt.CurrentStock < pp.Quantity * @woQty",
                        new { productId = (int)wo.ProductID, woQty = (int)wo.Quantity }, tx).ToList();

                    if (shortItems.Count > 0)
                    {
                        tx.Rollback();
                        var parts = string.Join("; ", shortItems.Select(b =>
                            $"{b.PartName} (need {b.Required:0.##}, have {b.OnHand})"));
                        throw new InvalidOperationException($"Insufficient stock: {parts}");
                    }
                }
            }

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
