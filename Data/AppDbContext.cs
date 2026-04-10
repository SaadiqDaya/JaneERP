using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JaneERP.Models;

namespace JaneERP.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<OrderEntity> Orders    { get; set; } = null!;
        public DbSet<LineItemEntity> LineItems { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs   { get; set; } = null!;

        private static string DbPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JaneERP", "app.db");

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
            options.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Adds StoreDomain column to the Orders table if it doesn't exist yet (SQLite has no IF NOT EXISTS for columns).
        /// Call once at startup after EnsureCreated().
        /// </summary>
        public void MigrateSchema()
        {
            try
            {
                Database.ExecuteSqlRaw("ALTER TABLE Orders ADD COLUMN StoreDomain TEXT NULL");
            }
            catch { /* column already exists — safe to ignore */ }
        }

        public async Task UpsertOrdersAsync(IEnumerable<Order> orders, string? storeDomain = null)
        {
            foreach (var o in orders)
            {
                var existing = await Orders.FindAsync(o.Id);
                if (existing == null)
                {
                    await Orders.AddAsync(new OrderEntity
                    {
                        Id             = o.Id,
                        OrderNumber    = o.OrderNumber,
                        Name           = o.Name,
                        CreatedAt      = o.CreatedAt,
                        UpdatedAt      = DateTime.UtcNow,
                        TotalPrice     = o.TotalPrice,
                        ShippingMethod = o.ShippingMethod,
                        Currency       = o.Currency,
                        ContactEmail   = o.ContactEmail,
                        StoreDomain    = storeDomain,
                        RawJson        = System.Text.Json.JsonSerializer.Serialize(o)
                    });
                }
                else
                {
                    existing.OrderNumber    = o.OrderNumber;
                    existing.Name           = o.Name;
                    existing.CreatedAt      = o.CreatedAt;
                    existing.TotalPrice     = o.TotalPrice;
                    existing.ShippingMethod = o.ShippingMethod;
                    existing.Currency       = o.Currency;
                    existing.ContactEmail   = o.ContactEmail;
                    existing.StoreDomain    = storeDomain ?? existing.StoreDomain;
                    existing.RawJson        = System.Text.Json.JsonSerializer.Serialize(o);
                    existing.UpdatedAt      = DateTime.UtcNow;
                }
            }
            await SaveChangesAsync();
        }

        public async Task UpsertOrderDetailsAsync(OrderDetails details)
        {
            if (details == null) return;

            var existing = await Orders.FindAsync(details.Id);
            if (existing == null)
            {
                existing = new OrderEntity
                {
                    Id           = details.Id,
                    OrderNumber  = details.OrderNumber,
                    Name         = details.Name,
                    CreatedAt    = details.CreatedAt,
                    UpdatedAt    = DateTime.UtcNow,
                    TotalPrice   = details.TotalPrice,
                    Currency     = details.Currency,
                    ContactEmail = details.ContactEmail,
                    RawJson      = System.Text.Json.JsonSerializer.Serialize(details)
                };
                await Orders.AddAsync(existing);
            }
            else
            {
                existing.OrderNumber  = details.OrderNumber;
                existing.Name         = details.Name;
                existing.CreatedAt    = details.CreatedAt;
                existing.TotalPrice   = details.TotalPrice;
                existing.Currency     = details.Currency;
                existing.ContactEmail = details.ContactEmail;
                existing.RawJson      = System.Text.Json.JsonSerializer.Serialize(details);
                existing.UpdatedAt    = DateTime.UtcNow;
            }

            var existingItems = LineItems.Where(li => li.OrderId == details.Id);
            LineItems.RemoveRange(existingItems);

            foreach (var li in details.LineItems)
            {
                await LineItems.AddAsync(new LineItemEntity
                {
                    Id       = li.Id,
                    OrderId  = details.Id,
                    Title    = li.Title,
                    Sku      = li.Sku,
                    Quantity = li.Quantity,
                    Price    = li.Price
                });
            }

            await SaveChangesAsync();
        }

        public List<Order> GetCachedOrders(string? storeDomain = null)
        {
            var query = Orders.AsQueryable();
            if (!string.IsNullOrEmpty(storeDomain))
                query = query.Where(o => o.StoreDomain == storeDomain);

            return query
                .OrderByDescending(o => o.CreatedAt)
                .Select(e => new Order
                {
                    Id             = e.Id,
                    Name           = e.Name,
                    OrderNumber    = e.OrderNumber,
                    CreatedAt      = e.CreatedAt,
                    TotalPrice     = e.TotalPrice,
                    ShippingMethod = e.ShippingMethod,
                    Currency       = e.Currency,
                    ContactEmail   = e.ContactEmail,
                    StoreDomain    = e.StoreDomain
                })
                .ToList();
        }
    }

    public class OrderEntity
    {
        public long      Id             { get; set; }
        public int       OrderNumber    { get; set; }
        public string?   Name           { get; set; }
        public DateTime  CreatedAt      { get; set; }
        public DateTime  UpdatedAt      { get; set; }
        public decimal   TotalPrice     { get; set; }
        public string?   ShippingMethod { get; set; }
        public string?   Currency       { get; set; }
        public string?   ContactEmail   { get; set; }
        public string?   StoreDomain    { get; set; }
        public string?   RawJson        { get; set; }
    }

    public class LineItemEntity
    {
        public long    Id       { get; set; }
        public long    OrderId  { get; set; }
        public string? Title    { get; set; }
        public string? Sku      { get; set; }
        public int     Quantity { get; set; }
        public decimal Price    { get; set; }
    }

    public class AuditLog
    {
        public int      Id      { get; set; }
        public DateTime When    { get; set; }
        public string?  User    { get; set; }
        public string?  Action  { get; set; }
        public string?  Details { get; set; }
    }
}
