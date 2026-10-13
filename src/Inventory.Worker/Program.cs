using Inventory.Worker.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging;

namespace Inventory.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddServiceBus(builder.Configuration);

            // PostgreSQL: la cadena se lee de "ConnectionStrings:InventoryDb"
            // (en local apunta al contenedor de docker-compose.yml, ver appsettings.Development.json).
            var inventoryDb = builder.Configuration.GetConnectionString("InventoryDb");
            if (string.IsNullOrWhiteSpace(inventoryDb))
            {
                throw new InvalidOperationException(
                    "Falta 'ConnectionStrings:InventoryDb'. Configúrala en appsettings.Development.json o con: dotnet user-secrets set \"ConnectionStrings:InventoryDb\" \"<cadena>\"");
            }
            // EnableRetryOnFailure reintenta errores transitorios (p. ej. el contenedor de Postgres todavía arrancando).
            builder.Services.AddDbContext<InventoryDbContext>(options =>
                options.UseNpgsql(inventoryDb, npgsql => npgsql.EnableRetryOnFailure()));

            builder.Services.AddScoped<StockService>();
            builder.Services.AddHostedService<OrderEventsConsumer>();

            var host = builder.Build();
            host.Run();
        }
    }
}
