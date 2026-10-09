using Azure.Messaging.ServiceBus;
using Shared.Contracts;
using Shared.Contracts.Events;

namespace Notifications.Worker
{
    /// <summary>
    /// Escucha topic "order-events", subscription "notifications"
    /// (filtro: Subject = OrderConfirmed | OrderRejected) y "envía" un email al cliente.
    /// </summary>
    public class OrderEventsConsumer : BackgroundService
    {
        private readonly ServiceBusClient _client;
        private readonly ILogger<OrderEventsConsumer> _logger;

        public OrderEventsConsumer(ServiceBusClient client, ILogger<OrderEventsConsumer> logger)
        {
            _client = client;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var processor = _client.CreateProcessor(Topics.OrderEvents, Subscriptions.Notifications, new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            });

            processor.ProcessMessageAsync += HandleMessageAsync;
            processor.ProcessErrorAsync += HandleErrorAsync;

            await processor.StartProcessingAsync(stoppingToken);
            _logger.LogInformation("Escuchando {Topic}/{Subscription}", Topics.OrderEvents, Subscriptions.Notifications);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // La app se está apagando.
            }

            await processor.StopProcessingAsync();
        }

        private async Task HandleMessageAsync(ProcessMessageEventArgs args)
        {
            var message = args.Message;
            _logger.LogInformation("Recibido {Subject} (MessageId {MessageId}, intento {DeliveryCount})",
                message.Subject, message.MessageId, message.DeliveryCount);

            switch (message.Subject)
            {
                case Subjects.OrderConfirmed:
                    var confirmed = message.Body.ToObjectFromJson<OrderConfirmed>()!;
                    _logger.LogInformation("Email a {Email}: tu pedido {OrderId} fue confirmado",
                        confirmed.CustomerEmail, confirmed.OrderId);
                    break;

                case Subjects.OrderRejected:
                    var rejected = message.Body.ToObjectFromJson<OrderRejected>()!;
                    _logger.LogInformation("Email a {Email}: tu pedido {OrderId} fue rechazado. Motivo: {Reason}",
                        rejected.CustomerEmail, rejected.OrderId, rejected.Reason);
                    break;

                default:
                    await args.DeadLetterMessageAsync(message, "UnexpectedSubject", $"Subject '{message.Subject}' no esperado");
                    return;
            }

            await args.CompleteMessageAsync(message, args.CancellationToken);
        }

        private Task HandleErrorAsync(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Error en {EntityPath} ({ErrorSource})", args.EntityPath, args.ErrorSource);
            return Task.CompletedTask;
        }
    }
}
