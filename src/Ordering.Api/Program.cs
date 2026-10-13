using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using OpenTelemetry.Trace;
using Ordering.Api.Messaging;
using Shared.Messaging;
using Shared.Observability;

namespace Ordering.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers()
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // OpenTelemetry → Datadog Agent: trazas, métricas y logs (Shared.Observability)
            builder.AddObservability("ordering-api", tracing => tracing.AddAspNetCoreInstrumentation());

            // Service Bus: cliente + publisher (Shared.Messaging)
            builder.Services.AddServiceBus(builder.Configuration);

            // PostgreSQL: la cadena se lee de "ConnectionStrings:OrderingDb"
            // (en local apunta al contenedor de docker-compose.yml, ver appsettings.Development.json).
            var orderingDb = builder.Configuration.GetConnectionString("OrderingDb");
            if (string.IsNullOrWhiteSpace(orderingDb))
            {
                throw new InvalidOperationException(
                    "Falta 'ConnectionStrings:OrderingDb'. Configúrala en appsettings.Development.json o con: dotnet user-secrets set \"ConnectionStrings:OrderingDb\" \"<cadena>\"");
            }
            // EnableRetryOnFailure reintenta errores transitorios (p. ej. el contenedor de Postgres todavía arrancando).
            builder.Services.AddDbContext<OrderingDbContext>(options =>
                options.UseNpgsql(orderingDb, npgsql => npgsql.EnableRetryOnFailure()));

            // Consumer de "inventory-events" corriendo en segundo plano dentro de la misma API
            builder.Services.AddHostedService<InventoryEventsConsumer>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
