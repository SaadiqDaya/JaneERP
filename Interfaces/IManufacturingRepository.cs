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
        List<Models.ReservationLine> GetWOReservationItems(int workOrderId);
        void                      SaveWOReservations(int workOrderId, IEnumerable<Models.ReservationLine> lines);
    }
}
