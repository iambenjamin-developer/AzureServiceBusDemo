using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Messaging;
using Shared.Messaging;

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

            // Service Bus: cliente + publisher (Shared.Messaging)
            builder.Services.AddServiceBus(builder.Configuration);

            // Azure SQL: la cadena se lee de "ConnectionStrings:OrderingDb" (user secrets en local).
            var orderingDb = builder.Configuration.GetConnectionString("OrderingDb");
            if (string.IsNullOrWhiteSpace(orderingDb))
            {
                throw new InvalidOperationException(
                    "Falta 'ConnectionStrings:OrderingDb'. Configúrala con: dotnet user-secrets set \"ConnectionStrings:OrderingDb\" \"<cadena>\"");
            }
            // EnableRetryOnFailure reintenta errores transitorios de Azure SQL (p. ej. base serverless que se está "despertando").
            builder.Services.AddDbContext<OrderingDbContext>(options =>
                options.UseSqlServer(orderingDb, sql => sql.EnableRetryOnFailure()));

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
