using JaneERP.Manufacturing;
using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IManufacturingRepository
    {
        List<ManufacturingOrder>  GetOrders(bool openOnly = false);
        ManufacturingOrder?       GetOrder(int moid);
        int                       CreateOrder(ManufacturingOrder mo);
        void                      UpdateOrderStatus(int moid, string status);
        List<WorkOrder>           GetPendingWorkOrders(DateTime? from = null, DateTime? to = null);
        void                      CompleteWorkOrder(int workOrderId, string? notes = null);
        void                      PartialCompleteWorkOrder(int workOrderId, int completedQty, int scrapQty = 0, string? scrapReason = null, string? notes = null);
        void                      UpdateWorkOrderStatus(int workOrderId, string status);
        void                      AssignWorkOrder(int workOrderId, string? assignedTo);
        List<NegativePartInfo>    GetNegativePartsForWorkOrder(int workOrderId);
        Dictionary<int, int>      GetReservedPartsQty();
        List<Models.ReservationLine>  GetWOReservationItems(int workOrderId);
        void                          SaveWOReservations(int workOrderId, IEnumerable<Models.ReservationLine> lines);
        List<Models.WOBomPreviewRow>  GetWOBomPreview(int workOrderId);

        // ── Cook Sessions ──────────────────────────────────────────────────────
        int                                   CreateCookSession(string sessionName, IEnumerable<int> workOrderIds, decimal batchLossPercent = 0m, string? createdBy = null);
        Models.CookSession?                   GetCookSession(int cookSessionId);
        List<Models.CookSession>              GetOpenCookSessions();
        List<Models.CookSessionStep>          GetCookSessionSteps(int cookSessionId);
        List<Models.CookIngredientSummary>    GetCookIngredients(int cookSessionId);
        void                                  MarkStepDone(int stepId, string doneBy);
        void                                  MarkAllIngredientStepsDone(int cookSessionId, int partId, string doneBy);
        void                                  CompleteCookSession(int cookSessionId, bool forceComplete = false);
        List<Models.BatchTravellerRow>        GetBatchTravellerData(int cookSessionId);
        List<Models.LabelExportRow>           GetLabelExportData(int cookSessionId);

        /// <summary>
        /// Deducts ingredient stock (Parts.CurrentStock) for all ingredients used in a completed cook session.
        /// Quantities come from the pre-computed CookSessionSteps.RequiredQtyML values aggregated per part.
        /// Returns true on success, false if the transaction fails.
        /// </summary>
        bool DeductSessionIngredients(int sessionId);

        /// <summary>
        /// Atomically marks a cook session Complete AND deducts all ingredient stock in a single transaction.
        /// Prefer this over calling CompleteCookSession + DeductSessionIngredients separately.
        /// Throws InvalidOperationException if steps are still pending and forceComplete is false.
        /// Returns true on success; returns false (and logs) if a DB error occurs.
        /// </summary>
        bool CompleteCookSessionAndDeductStock(int cookSessionId, bool forceComplete = false, string? completedBy = null);
    }
}
