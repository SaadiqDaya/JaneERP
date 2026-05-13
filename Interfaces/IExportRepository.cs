namespace JaneERP.Interfaces
{
    public interface IExportRepository
    {
        IEnumerable<dynamic> GetProductsForExport();
        IEnumerable<dynamic> GetInventoryByLocationForExport();
        IEnumerable<dynamic> GetReorderSummaryForExport();
        IEnumerable<dynamic> GetPartsForExport();
        IEnumerable<dynamic> GetSalesOrdersForExport(DateTime from, DateTime to);
        IEnumerable<dynamic> GetSalesOrderLineItemsForExport(DateTime from, DateTime to);
        IEnumerable<dynamic> GetPurchaseOrdersForExport(DateTime from, DateTime to);
        IEnumerable<dynamic> GetWorkOrdersForExport();
        IEnumerable<dynamic> GetCogsSummaryForExport();
        IEnumerable<dynamic> GetCustomerListForExport();
    }
}
