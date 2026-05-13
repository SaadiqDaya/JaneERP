using JaneERP.Data;
using JaneERP.Interfaces;
using JaneERP.Manufacturing;
using JaneERP.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaneERP.Infrastructure
{
    /// <summary>
    /// Registers all repositories and services into the DI container and initializes
    /// <see cref="AppServices"/> so forms can resolve dependencies via
    /// <c>AppServices.Get&lt;T&gt;()</c>.
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// Call once from Program.Main, after the company/database has been selected
        /// (connection string is available) and before any form that uses a repository is opened.
        /// </summary>
        public static void Build()
        {
            var services = new ServiceCollection();

            // ── Repositories ──────────────────────────────────────────────────────
            services.AddSingleton<IProductRepository,      ProductRepository>();
            services.AddSingleton<IPartRepository,         PartRepository>();
            services.AddSingleton<IVendorRepository,       VendorRepository>();
            services.AddSingleton<ISupplierRepository,     SupplierRepository>();
            services.AddSingleton<IUserRepository,         UserRepository>();
            services.AddSingleton<ITaskRepository,         TaskRepository>();
            services.AddSingleton<ILocationRepository,     LocationRepository>();
            services.AddSingleton<IStoreRepository,        StoreRepository>();
            services.AddSingleton<IPackageRepository,      PackageRepository>();
            services.AddSingleton<IProductTypeRepository,  ProductTypeRepository>();
            services.AddSingleton<IDiscountTierRepository, DiscountTierRepository>();
            services.AddSingleton<ICycleCountRepository,   CycleCountRepository>();

            // ── Manufacturing ─────────────────────────────────────────────────────
            services.AddSingleton<IManufacturingRepository, ManufacturingRepository>();

            // ── Services ──────────────────────────────────────────────────────────
            services.AddSingleton<IShopifySyncService, ShopifySyncService>();
            services.AddSingleton<ShopifyClient>();

            AppServices.Initialize(services.BuildServiceProvider());
        }
    }
}
