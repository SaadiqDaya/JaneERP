using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiCookingRepository
{
    private readonly CompanyContext              _ctx;
    private readonly IConfiguration              _config;
    private readonly ILogger<ApiCookingRepository> _logger;

    public ApiCookingRepository(CompanyContext ctx, IConfiguration config, ILogger<ApiCookingRepository> logger)
    {
        _ctx    = ctx;
        _config = config;
        _logger = logger;
    }

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    // ── Manufacturing settings helpers ────────────────────────────────────────

    public ManufacturingSettingsResponse GetManufacturingSettings()
    {
        var flasks = _config.GetSection("ManufacturingSettings:FlaskConfigs")
            .Get<List<FlaskConfigDto>>() ?? DefaultFlasks();
        var presets = _config.GetSection("ManufacturingSettings:BatchLossPresets")
            .Get<List<BatchLossPresetDto>>() ?? DefaultPresets();
        return new ManufacturingSettingsResponse { FlaskConfigs = flasks, BatchLossPresets = presets };
    }

    private string GetFlaskForBatchMl(decimal batchMl)
    {
        var sorted = (
            _config.GetSection("ManufacturingSettings:FlaskConfigs").Get<List<FlaskConfigDto>>()
            ?? DefaultFlasks()
        ).OrderBy(f => f.MaxBatchMl).ToList();

        foreach (var fc in sorted)
            if (batchMl <= fc.MaxBatchMl) return fc.Name;
        return sorted.LastOrDefault()?.Name ?? "Unknown";
    }

    private static List<FlaskConfigDto> DefaultFlasks() =>
    [
        new FlaskConfigDto { Name = "1L Squeeze",    MaxBatchMl = 1_000   },
        new FlaskConfigDto { Name = "10L Jug",       MaxBatchMl = 9_000   },
        new FlaskConfigDto { Name = "20L Stainless", MaxBatchMl = 18_000  },
        new FlaskConfigDto { Name = "100L Vat",      MaxBatchMl = 999_999 },
    ];

    private static List<BatchLossPresetDto> DefaultPresets() =>
    [
        new BatchLossPresetDto { Label = "10ml / 30ml bottles", Percent = 15m },
        new BatchLossPresetDto { Label = "30ml Glass",          Percent = 7m  },
        new BatchLossPresetDto { Label = "60ml bottles",        Percent = 6m  },
        new BatchLossPresetDto { Label = "120ml+",              Percent = 3m  },
    ];

    // ── Sessions ──────────────────────────────────────────────────────────────

    public List<CookSessionSummary> GetOpenSessions()
    {
        using var db = Connect();
        try
        {
            return db.Query<CookSessionSummary>(@"
                SELECT cs.CookSessionID, cs.SessionName, cs.Status,
                       cs.CreatedBy, cs.CreatedAt, cs.CompletedAt,
                       ISNULL(cs.BatchLossPercent, 0)              AS BatchLossPercent,
                       COUNT(s.StepID)                             AS TotalSteps,
                       ISNULL(SUM(CAST(s.IsDone AS INT)), 0)       AS DoneSteps
                FROM   CookSessions cs
                LEFT JOIN CookSessionSteps s ON s.CookSessionID = cs.CookSessionID
                WHERE  cs.Status = 'Open'
                GROUP BY cs.CookSessionID, cs.SessionName, cs.Status,
                         cs.CreatedBy, cs.CreatedAt, cs.CompletedAt, cs.BatchLossPercent
                ORDER BY cs.CreatedAt DESC").ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiCookingRepository.GetOpenSessions] Query failed"); return []; }
    }

    public CookSessionDetail? GetSession(int cookSessionId)
    {
        using var db = Connect();
        try
        {
            var session = db.QueryFirstOrDefault<CookSessionDetail>(@"
                SELECT CookSessionID, SessionName, Status, CreatedBy, CreatedAt, CompletedAt,
                       ISNULL(BatchLossPercent, 0) AS BatchLossPercent
                FROM   CookSessions
                WHERE  CookSessionID = @cookSessionId",
                new { cookSessionId });
            if (session == null) return null;

            // One summary row per ingredient — use stored RequiredQtyML when available
            var ingredients = db.Query<CookIngredientDto>(@"
                SELECT p.PartID, p.PartNumber, p.PartName, p.UnitOfMeasure,
                       SUM(COALESCE(s.RequiredQtyML, pp.Quantity * wo.Quantity)) AS TotalRequired,
                       p.CurrentStock                                             AS OnHand,
                       ISNULL(SUM(CAST(s.IsDone AS INT)), 0)                     AS StepsDone,
                       COUNT(s.StepID)                                           AS StepsTotal,
                       p.Density
                FROM   CookSessionSteps s
                JOIN   Parts        p  ON p.PartID      = s.PartID
                JOIN   WorkOrders   wo ON wo.WorkOrderID = s.WorkOrderID
                JOIN   ProductParts pp ON pp.ProductID   = wo.ProductID AND pp.PartID = s.PartID
                WHERE  s.CookSessionID = @cookSessionId
                GROUP BY p.PartID, p.PartNumber, p.PartName, p.UnitOfMeasure, p.CurrentStock, p.Density
                ORDER BY p.PartName",
                new { cookSessionId }).ToList();

            // Per-batch steps — use stored RequiredQtyML, include FlaskType from CookSessionBatches
            var steps = db.Query<CookStepDto>(@"
                SELECT s.StepID, s.WorkOrderID, s.PartID,
                       s.IsDone, s.DoneBy, s.DoneAt,
                       p.ProductName,
                       mo.MONumber,
                       wo.Quantity                                              AS WorkOrderQty,
                       COALESCE(s.RequiredQtyML, pp.Quantity * wo.Quantity)    AS RequiredQty,
                       csb.FlaskType,
                       pt.Density
                FROM   CookSessionSteps    s
                JOIN   WorkOrders          wo  ON wo.WorkOrderID = s.WorkOrderID
                JOIN   Products            p   ON p.ProductID   = wo.ProductID
                JOIN   ManufacturingOrders mo  ON mo.MOID       = wo.MOID
                JOIN   ProductParts        pp  ON pp.ProductID  = wo.ProductID AND pp.PartID = s.PartID
                JOIN   Parts               pt  ON pt.PartID     = s.PartID
                LEFT JOIN CookSessionBatches csb ON csb.CookSessionID = s.CookSessionID
                                                 AND csb.WorkOrderID  = s.WorkOrderID
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
        catch (Exception ex) { _logger.LogError(ex, "[ApiCookingRepository.GetSession] Query failed for CookSessionID={Id}", cookSessionId); return null; }
    }

    /// <summary>
    /// Work orders available for a new cook session.
    /// Returns Live (inventory locked, ready to cook) and InProgress (already cooking).
    /// </summary>
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
                WHERE  wo.Status IN ('Live', 'InProgress', 'In Progress')
                ORDER  BY
                    CASE wo.Status WHEN 'InProgress' THEN 0 WHEN 'In Progress' THEN 0 ELSE 1 END,
                    wo.WorkOrderID").ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiCookingRepository.GetPendingWorkOrders] Query failed"); return []; }
    }

    public int CreateSession(string sessionName, IEnumerable<int> workOrderIds,
        decimal batchLossPercent, string createdBy)
    {
        var woIds = workOrderIds.ToList();
        if (woIds.Count == 0) throw new ArgumentException("At least one work order required.");

        decimal lossMultiplier = 1m + (batchLossPercent / 100m);

        using var db = new SqlConnection(_ctx.ConnectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            int sessionId = db.QuerySingle<int>(@"
                INSERT INTO CookSessions (SessionName, CreatedBy, BatchLossPercent)
                VALUES (@sessionName, @createdBy, @batchLossPercent);
                SELECT CAST(SCOPE_IDENTITY() AS INT)",
                new { sessionName, createdBy, batchLossPercent }, tx);

            foreach (int woId in woIds)
            {
                var woInfo = db.QueryFirstOrDefault(@"
                    SELECT wo.Quantity, wo.ProductID, wo.Status
                    FROM   WorkOrders wo
                    WHERE  wo.WorkOrderID = @woId", new { woId }, tx);
                if (woInfo == null) continue;

                // Advance Live → InProgress when cooking begins
                if ((string)woInfo.Status == "Live")
                {
                    db.Execute("UPDATE WorkOrders SET Status = 'InProgress' WHERE WorkOrderID = @woId",
                        new { woId }, tx);
                }

                int woQty     = (int)woInfo.Quantity;
                int productId = (int)woInfo.ProductID;

                // SizeML stored as a ProductAttribute (Manufacturing/Number)
                string? sizeMlStr = db.QueryFirstOrDefault<string>(@"
                    SELECT AttributeValue
                    FROM   ProductAttributes
                    WHERE  ProductID = @productId AND AttributeName = 'SizeML'",
                    new { productId }, tx);

                decimal sizeMl = decimal.TryParse(sizeMlStr,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

                decimal batchSizeMl = woQty * sizeMl * lossMultiplier;
                string  flaskType   = batchSizeMl > 0 ? GetFlaskForBatchMl(batchSizeMl) : "";

                db.Execute(@"
                    INSERT INTO CookSessionBatches (CookSessionID, WorkOrderID, FlaskType, BatchSizeML)
                    VALUES (@sessionId, @woId, @flaskType, @batchSizeMl)",
                    new { sessionId, woId, flaskType, batchSizeMl }, tx);

                // Insert one step per BOM part with pre-computed RequiredQtyML
                // Per-row batch loss: CreatesBatchLoss controls whether loss applies;
                // BatchLossRate overrides the session rate when > 0.
                var bomParts = db.Query(@"
                    SELECT pp.PartID, pp.Quantity AS BomQty,
                           pp.CreatesBatchLoss, pp.BatchLossRate
                    FROM   WorkOrders   wo
                    JOIN   ProductParts pp ON pp.ProductID = wo.ProductID
                    WHERE  wo.WorkOrderID = @woId", new { woId }, tx).ToList();

                foreach (var part in bomParts)
                {
                    decimal effectiveRate = (bool)part.CreatesBatchLoss
                        ? ((decimal)part.BatchLossRate > 0m ? (decimal)part.BatchLossRate : batchLossPercent)
                        : 0m;
                    decimal reqQty = (decimal)part.BomQty * woQty * (1m + effectiveRate / 100m);
                    db.Execute(@"
                        INSERT INTO CookSessionSteps (CookSessionID, WorkOrderID, PartID, RequiredQtyML)
                        VALUES (@sessionId, @woId, @partId, @reqQty)",
                        new { sessionId, woId, partId = (int)part.PartID, reqQty }, tx);
                }
            }

            tx.Commit();

            try
            {
                db.Execute(@"
                    INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
                    VALUES (@user, 'CreateCookSession', @details, GETDATE())",
                    new
                    {
                        user    = createdBy,
                        details = $"CookSessionID={sessionId} SessionName={sessionName} WorkOrders=[{string.Join(",", woIds)}] BatchLossPercent={batchLossPercent}"
                    });
            }
            catch (Exception auditEx) { _logger.LogError(auditEx, "[ApiCookingRepository.CreateSession] Audit insert failed for CookSessionID={Id}", sessionId); }

            return sessionId;
        }
        catch { tx.Rollback(); throw; }
    }

    public void MarkStepDone(int stepId, string doneBy)
    {
        using var db = Connect();
        var rows = db.Execute(@"
            UPDATE CookSessionSteps
            SET IsDone = 1, DoneBy = @doneBy, DoneAt = GETDATE()
            WHERE StepID = @stepId AND IsDone = 0",
            new { stepId, doneBy });

        if (rows > 0)
        {
            try
            {
                db.Execute(@"
                    INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
                    VALUES (@user, 'MarkCookStepDone', @details, GETDATE())",
                    new { user = doneBy, details = $"StepID={stepId}" });
            }
            catch (Exception auditEx) { _logger.LogError(auditEx, "[ApiCookingRepository.MarkStepDone] Audit insert failed for StepID={Id}", stepId); }
        }
    }

    public void MarkAllIngredientStepsDone(int cookSessionId, int partId, string doneBy)
    {
        using var db = Connect();
        var rows = db.Execute(@"
            UPDATE CookSessionSteps
            SET IsDone = 1, DoneBy = @doneBy, DoneAt = GETDATE()
            WHERE CookSessionID = @cookSessionId AND PartID = @partId AND IsDone = 0",
            new { cookSessionId, partId, doneBy });

        if (rows > 0)
        {
            try
            {
                db.Execute(@"
                    INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
                    VALUES (@user, 'MarkAllIngredientStepsDone', @details, GETDATE())",
                    new { user = doneBy, details = $"CookSessionID={cookSessionId} PartID={partId} StepsMarked={rows}" });
            }
            catch (Exception auditEx) { _logger.LogError(auditEx, "[ApiCookingRepository.MarkAllIngredientStepsDone] Audit insert failed for CookSessionID={Id}", cookSessionId); }
        }
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

        try
        {
            db.Execute(@"
                INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
                VALUES (@user, 'CompleteCookSession', @details, GETDATE())",
                new
                {
                    user    = completedBy,
                    details = $"CookSessionID={cookSessionId} ForceComplete={forceComplete}"
                });
        }
        catch (Exception auditEx) { _logger.LogError(auditEx, "[ApiCookingRepository.CompleteSession] Audit insert failed for CookSessionID={Id}", cookSessionId); }
    }
}
