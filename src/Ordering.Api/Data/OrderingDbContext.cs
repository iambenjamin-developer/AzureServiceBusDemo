using Microsoft.EntityFrameworkCore;
using Ordering.Api.Models;

namespace Ordering.Api.Data
{
    public class OrderingDbContext : DbContext
    {
        public OrderingDbContext(DbContextOptions<OrderingDbContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(order =>
            {
                order.Property(o => o.CustomerEmail).HasMaxLength(256);
                // El enum se guarda como texto ("Pending", "Confirmed"...) para que se lea bien en la tabla.
                order.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
                order.Property(o => o.RejectionReason).HasMaxLength(500);
                order.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
            });

            modelBuilder.Entity<OrderLine>().ToTable("OrderLines");
        }
    }
}
