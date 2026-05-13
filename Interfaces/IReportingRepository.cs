namespace JaneERP.Interfaces
{
    /// <summary>
    /// Read-only reporting queries. All methods return data suitable for binding directly to
    /// DataGridViews or serialising to JSON for API consumers.
    /// </summary>
    public interface IReportingRepository
    {
        /// <summary>Stock on hand by location and product, with retail and wholesale values.</summary>
        IEnumerable<dynamic> GetStockOnHand();

        /// <summary>Sales orders within the given date range with customer and store info.</summary>
        IEnumerable<dynamic> GetSalesByPeriod(DateTime from, DateTime to);

        /// <summary>Completed work order COGS summary.</summary>
        IEnumerable<dynamic> GetCogsSummary();

        /// <summary>Cycle count variance records within the date range.</summary>
        IEnumerable<dynamic> GetCycleCountVariance(DateTime from, DateTime to);

        /// <summary>Gross profit by product within the date range (Revenue minus COGS).</summary>
        IEnumerable<dynamic> GetGrossProfitByProduct(DateTime from, DateTime to);

        /// <summary>Total row count in SalesOrderItems — used to diagnose empty GP reports.</summary>
        int GetSalesOrderItemCount();
    }
}
