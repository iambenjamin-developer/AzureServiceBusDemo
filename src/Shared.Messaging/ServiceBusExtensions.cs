using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Messaging;

public static class ServiceBusExtensions
{
    /// <summary>
    /// Registra un único ServiceBusClient (singleton, como recomienda Microsoft) y el publisher.
    /// Lee la cadena de conexión de "ConnectionStrings:ServiceBus".
    /// </summary>
    public static IServiceCollection AddServiceBus(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ServiceBus");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta 'ConnectionStrings:ServiceBus'. Configúrala con: dotnet user-secrets set \"ConnectionStrings:ServiceBus\" \"<cadena>\"");
        }

        // Se registra con factory para que el contenedor lo cierre (DisposeAsync) al apagar la app.
        services.AddSingleton(_ => new ServiceBusClient(connectionString));
        services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();

        return services;
    }
}
