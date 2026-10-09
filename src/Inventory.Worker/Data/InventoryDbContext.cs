using Microsoft.EntityFrameworkCore;

namespace Inventory.Worker.Data
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(product =>
            {
                // El Id lo decide el catálogo, no la base de datos (sin IDENTITY).
                product.Property(p => p.Id).ValueGeneratedNever();
                product.Property(p => p.Name).HasMaxLength(100);

                // Datos iniciales: se insertan con la migración.
                product.HasData(
                    new Product { Id = 1, Name = "Teclado", Stock = 10 },
                    new Product { Id = 2, Name = "Mouse", Stock = 5 },
                    new Product { Id = 3, Name = "Monitor", Stock = 0 });
            });
        }
    }
}
