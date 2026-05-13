using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IKPIRepository
    {
        KpiSummary GetKPIs();
    }
}
