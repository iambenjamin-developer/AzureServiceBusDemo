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

            // Azure SQL: la cadena se lee de "ConnectionStrings:InventoryDb" (user secrets en local).
            var inventoryDb = builder.Configuration.GetConnectionString("InventoryDb");
            if (string.IsNullOrWhiteSpace(inventoryDb))
            {
                throw new InvalidOperationException(
                    "Falta 'ConnectionStrings:InventoryDb'. Configúrala con: dotnet user-secrets set \"ConnectionStrings:InventoryDb\" \"<cadena>\"");
            }
            // EnableRetryOnFailure reintenta errores transitorios de Azure SQL (p. ej. base serverless que se está "despertando").
            builder.Services.AddDbContext<InventoryDbContext>(options =>
                options.UseSqlServer(inventoryDb, sql => sql.EnableRetryOnFailure()));

            builder.Services.AddScoped<StockService>();
            builder.Services.AddHostedService<OrderEventsConsumer>();

            var host = builder.Build();
            host.Run();
        }
    }
}
