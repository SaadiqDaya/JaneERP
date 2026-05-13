using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IDiscountTierRepository
    {
        IEnumerable<DiscountTier>  GetAll();
        IEnumerable<DiscountTier>  GetActive();
        int                        Add(DiscountTier tier);
        void                       Update(DiscountTier tier);
        void                       Deactivate(int id);
        void                       SetCustomerTier(int customerId, int? tierId);
        IEnumerable<dynamic>       GetCustomersWithTiers();
        DiscountTier?              GetTierForCustomer(int customerId);
    }
}
