using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Shared.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Configura OpenTelemetry (trazas, métricas y logs) y lo envía por OTLP al Datadog Agent
    /// (docker-compose.yml, puerto 4317). Para cambiar el destino: variable OTEL_EXPORTER_OTLP_ENDPOINT.
    /// </summary>
    /// <param name="serviceName">Aparece como "service" en Datadog (p. ej. "ordering-api").</param>
    /// <param name="configureTracing">Para añadir instrumentación propia de un proyecto (p. ej. ASP.NET Core).</param>
    public static IHostApplicationBuilder AddObservability(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        // El SDK de Azure solo emite trazas de Service Bus (y mete el trace_id dentro del mensaje)
        // si se activa este switch. Debe ejecutarse antes de crear cualquier ServiceBusClient.
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        builder.Services.AddOpenTelemetry()
            // Unified service tagging de Datadog: service + env (+ version).
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = builder.Environment.EnvironmentName.ToLowerInvariant() switch
                    {
                        "development" => "dev",
                        var env => env
                    }
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddHttpClientInstrumentation()   // llamadas HTTP salientes
                    .AddNpgsql()                      // consultas a PostgreSQL
                    .AddSource("Azure.*");            // envío y recepción en Service Bus

                configureTracing?.Invoke(tracing);
                tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();     // GC, memoria, threads

                configureMetrics?.Invoke(metrics);
                metrics.AddOtlpExporter();
            });

        // Logs: cada _logger.LogXxx también se envía, con el trace_id de la traza activa.
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter();
        });

        return builder;
    }
}
