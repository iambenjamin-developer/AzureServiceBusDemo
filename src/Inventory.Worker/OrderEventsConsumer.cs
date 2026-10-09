using Azure.Messaging.ServiceBus;
using Shared.Contracts;
using Shared.Contracts.Events;
using Shared.Messaging;

namespace Inventory.Worker
{
    /// <summary>
    /// Escucha topic "order-events", subscription "inventory" (filtro: Subject = OrderPlaced).
    /// Reserva stock y responde en el topic "inventory-events".
    /// </summary>
    public class OrderEventsConsumer : BackgroundService
    {
        private readonly ServiceBusClient _client;
        private readonly IEventPublisher _publisher;
        private readonly InMemoryStock _stock;
        private readonly ILogger<OrderEventsConsumer> _logger;

        public OrderEventsConsumer(ServiceBusClient client, IEventPublisher publisher, InMemoryStock stock, ILogger<OrderEventsConsumer> logger)
        {
            _client = client;
            _publisher = publisher;
            _stock = stock;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var processor = _client.CreateProcessor(Topics.OrderEvents, Subscriptions.Inventory, new ServiceBusProcessorOptions
            {
                // Completamos a mano para que se vea cuándo el mensaje se da por procesado.
                AutoCompleteMessages = false,
                // 1 mensaje a la vez: así InMemoryStock no necesita locks.
                MaxConcurrentCalls = 1
            });

            processor.ProcessMessageAsync += HandleMessageAsync;
            processor.ProcessErrorAsync += HandleErrorAsync;

            await processor.StartProcessingAsync(stoppingToken);
            _logger.LogInformation("Escuchando {Topic}/{Subscription}", Topics.OrderEvents, Subscriptions.Inventory);

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

            // El filtro de la subscription ya garantiza esto; si llega algo inesperado
            // no tiene sentido reintentarlo, así que va directo a la dead-letter queue.
            if (message.Subject != Subjects.OrderPlaced)
            {
                await args.DeadLetterMessageAsync(message, "UnexpectedSubject", $"Subject '{message.Subject}' no esperado");
                return;
            }

            var order = message.Body.ToObjectFromJson<OrderPlaced>()!;

            if (_stock.TryReserve(order.Items, out var reason))
            {
                await _publisher.PublishAsync(Topics.InventoryEvents, Subjects.StockReserved,
                    new StockReserved(order.OrderId), args.CancellationToken);
                _logger.LogInformation("Stock reservado para el pedido {OrderId}", order.OrderId);
            }
            else
            {
                await _publisher.PublishAsync(Topics.InventoryEvents, Subjects.StockRejected,
                    new StockRejected(order.OrderId, reason), args.CancellationToken);
                _logger.LogWarning("Stock rechazado para el pedido {OrderId}: {Reason}", order.OrderId, reason);
            }

            // Si algo de arriba lanza una excepción, NO llegamos aquí: el processor hace "abandon",
            // el mensaje vuelve a la subscription y se reintenta. Tras MaxDeliveryCount
            // intentos (10 por defecto) Service Bus lo mueve a la dead-letter queue.
            await args.CompleteMessageAsync(message, args.CancellationToken);
        }

        private Task HandleErrorAsync(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Error en {EntityPath} ({ErrorSource})", args.EntityPath, args.ErrorSource);
            return Task.CompletedTask;
        }
    }
}
