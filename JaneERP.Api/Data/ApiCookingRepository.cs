using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiCookingRepository
{
    private readonly CompanyContext _ctx;
    public ApiCookingRepository(CompanyContext ctx) => _ctx = ctx;

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public List<CookSessionSummary> GetOpenSessions()
    {
        using var db = Connect();
        try
        {
            return db.Query<CookSessionSummary>(@"
                SELECT cs.CookSessionID, cs.SessionName, cs.Status,
                       cs.CreatedBy, cs.CreatedAt, cs.CompletedAt,
                       COUNT(s.StepID)                       AS TotalSteps,
                       ISNULL(SUM(CAST(s.IsDone AS INT)), 0) AS DoneSteps
                FROM   CookSessions cs
                LEFT JOIN CookSessionSteps s ON s.CookSessionID = cs.CookSessionID
                WHERE  cs.Status = 'Open'
                GROUP BY cs.CookSessionID, cs.SessionName, cs.Status,
                         cs.CreatedBy, cs.CreatedAt, cs.CompletedAt
                ORDER BY cs.CreatedAt DESC").ToList();
        }
        catch { return []; }
    }

    public CookSessionDetail? GetSession(int cookSessionId)
    {
        using var db = Connect();
        try
        {
            var session = db.QueryFirstOrDefault<CookSessionDetail>(@"
                SELECT CookSessionID, SessionName, Status, CreatedBy, CreatedAt, CompletedAt
                FROM   CookSessions
                WHERE  CookSessionID = @cookSessionId",
                new { cookSessionId });
            if (session == null) return null;

            // One summary row per ingredient
            var ingredients = db.Query<CookIngredientDto>(@"
                SELECT p.PartID, p.PartNumber, p.PartName, p.UnitOfMeasure,
                       SUM(pp.Quantity * wo.Quantity)        AS TotalRequired,
                       p.CurrentStock                        AS OnHand,
                       ISNULL(SUM(CAST(s.IsDone AS INT)), 0) AS StepsDone,
                       COUNT(s.StepID)                       AS StepsTotal
                FROM   CookSessionSteps s
                JOIN   Parts        p  ON p.PartID      = s.PartID
                JOIN   WorkOrders   wo ON wo.WorkOrderID = s.WorkOrderID
                JOIN   ProductParts pp ON pp.ProductID   = wo.ProductID AND pp.PartID = s.PartID
                WHERE  s.CookSessionID = @cookSessionId
                GROUP BY p.PartID, p.PartNumber, p.PartName, p.UnitOfMeasure, p.CurrentStock
                ORDER BY p.PartName",
                new { cookSessionId }).ToList();

            // All steps, grouped client-side by PartID
            var steps = db.Query<CookStepDto>(@"
                SELECT s.StepID, s.WorkOrderID, s.PartID,
                       s.IsDone, s.DoneBy, s.DoneAt,
                       p.ProductName,
                       mo.MONumber,
                       wo.Quantity               AS WorkOrderQty,
                       pp.Quantity * wo.Quantity AS RequiredQty
                FROM   CookSessionSteps    s
                JOIN   WorkOrders          wo ON wo.WorkOrderID = s.WorkOrderID
                JOIN   Products            p  ON p.ProductID   = wo.ProductID
                JOIN   ManufacturingOrders mo ON mo.MOID       = wo.MOID
                JOIN   ProductParts        pp ON pp.ProductID  = wo.ProductID AND pp.PartID = s.PartID
                WHERE  s.CookSessionID = @cookSessionId
                ORDER BY s.PartID, s.WorkOrderID",
                new { cookSessionId }).ToList();

            var stepsByPart = steps.GroupBy(s => s.PartID)
                                   .ToDictionary(g => g.Key, g => g.ToList());
            foreach (var ingr in ingredients)
            {
                if (stepsByPart.TryGetValue(ingr.PartID, out var ingrSteps))
                    ingr.Steps = ingrSteps;
            }

            session.Ingredients = ingredients;
            return session;
        }
        catch { return null; }
    }

    public List<CookWorkOrderItem> GetPendingWorkOrders()
    {
        using var db = Connect();
        try
        {
            return db.Query<CookWorkOrderItem>(@"
                SELECT wo.WorkOrderID, mo.MONumber, p.ProductName, p.SKU,
                       wo.Quantity, wo.Status, wo.AssignedTo
                FROM   WorkOrders          wo
                JOIN   ManufacturingOrders mo ON mo.MOID    = wo.MOID
                JOIN   Products            p  ON p.ProductID = wo.ProductID
                WHERE  wo.Status NOT IN ('Complete','Completed','Cancelled')
                ORDER  BY wo.WorkOrderID").ToList();
        }
        catch { return []; }
    }

    public int CreateSession(string sessionName, IEnumerable<int> workOrderIds, string createdBy)
    {
        var woIds = workOrderIds.ToList();
        if (woIds.Count == 0) throw new ArgumentException("At least one work order required.");

        using var db = new SqlConnection(_ctx.ConnectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            int sessionId = db.QuerySingle<int>(@"
                INSERT INTO CookSessions (SessionName, CreatedBy)
                VALUES (@sessionName, @createdBy);
                SELECT CAST(SCOPE_IDENTITY() AS INT)",
                new { sessionName, createdBy }, tx);

            foreach (int woId in woIds)
            {
                db.Execute(
                    "INSERT INTO CookSessionBatches (CookSessionID, WorkOrderID) VALUES (@sessionId, @woId)",
                    new { sessionId, woId }, tx);

                db.Execute(@"
                    INSERT INTO CookSessionSteps (CookSessionID, WorkOrderID, PartID)
                    SELECT @sessionId, @woId, pp.PartID
                    FROM   WorkOrders   wo
                    JOIN   ProductParts pp ON pp.ProductID = wo.ProductID
                    WHERE  wo.WorkOrderID = @woId",
                    new { sessionId, woId }, tx);
            }

            tx.Commit();
            return sessionId;
        }
        catch { tx.Rollback(); throw; }
    }

    public void MarkStepDone(int stepId, string doneBy)
    {
        using var db = Connect();
        db.Execute(@"
            UPDATE CookSessionSteps
            SET IsDone = 1, DoneBy = @doneBy, DoneAt = GETDATE()
            WHERE StepID = @stepId AND IsDone = 0",
            new { stepId, doneBy });
    }

    public void MarkAllIngredientStepsDone(int cookSessionId, int partId, string doneBy)
    {
        using var db = Connect();
        db.Execute(@"
            UPDATE CookSessionSteps
            SET IsDone = 1, DoneBy = @doneBy, DoneAt = GETDATE()
            WHERE CookSessionID = @cookSessionId AND PartID = @partId AND IsDone = 0",
            new { cookSessionId, partId, doneBy });
    }

    public void CompleteSession(int cookSessionId, bool forceComplete, string completedBy)
    {
        using var db = Connect();
        if (!forceComplete)
        {
            int pending = db.QuerySingle<int>(
                "SELECT COUNT(*) FROM CookSessionSteps WHERE CookSessionID = @cookSessionId AND IsDone = 0",
                new { cookSessionId });
            if (pending > 0)
                throw new InvalidOperationException($"{pending} step(s) still pending.");
        }
        db.Execute(@"
            UPDATE CookSessions SET Status = 'Complete', CompletedAt = GETDATE()
            WHERE CookSessionID = @cookSessionId",
            new { cookSessionId });
    }
}
