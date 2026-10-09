using Azure.Messaging.ServiceBus;
using Ordering.Api.Models;
using Shared.Contracts;
using Shared.Contracts.Events;
using Shared.Messaging;

namespace Ordering.Api.Messaging
{
    /// <summary>
    /// Escucha topic "inventory-events", subscription "ordering"
    /// (filtro: Subject = StockReserved | StockRejected).
    /// Actualiza el pedido y publica OrderConfirmed / OrderRejected en "order-events".
    /// </summary>
    public class InventoryEventsConsumer : BackgroundService
    {
        private readonly ServiceBusClient _client;
        private readonly IEventPublisher _publisher;
        private readonly OrderStore _store;
        private readonly ILogger<InventoryEventsConsumer> _logger;

        public InventoryEventsConsumer(ServiceBusClient client, IEventPublisher publisher, OrderStore store, ILogger<InventoryEventsConsumer> logger)
        {
            _client = client;
            _publisher = publisher;
            _store = store;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var processor = _client.CreateProcessor(Topics.InventoryEvents, Subscriptions.Ordering, new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            });

            processor.ProcessMessageAsync += HandleMessageAsync;
            processor.ProcessErrorAsync += HandleErrorAsync;

            await processor.StartProcessingAsync(stoppingToken);
            _logger.LogInformation("Escuchando {Topic}/{Subscription}", Topics.InventoryEvents, Subscriptions.Ordering);

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
                case Subjects.StockReserved:
                    var reserved = message.Body.ToObjectFromJson<StockReserved>()!;
                    var confirmedOrder = _store.Get(reserved.OrderId);
                    if (confirmedOrder is null)
                    {
                        await DeadLetterOrderNotFoundAsync(args, reserved.OrderId);
                        return;
                    }

                    confirmedOrder.Status = OrderStatus.Confirmed;
                    await _publisher.PublishAsync(Topics.OrderEvents, Subjects.OrderConfirmed,
                        new OrderConfirmed(confirmedOrder.Id, confirmedOrder.CustomerEmail), args.CancellationToken);
                    _logger.LogInformation("Pedido {OrderId} confirmado", confirmedOrder.Id);
                    break;

                case Subjects.StockRejected:
                    var stockRejected = message.Body.ToObjectFromJson<StockRejected>()!;
                    var rejectedOrder = _store.Get(stockRejected.OrderId);
                    if (rejectedOrder is null)
                    {
                        await DeadLetterOrderNotFoundAsync(args, stockRejected.OrderId);
                        return;
                    }

                    rejectedOrder.Status = OrderStatus.Rejected;
                    rejectedOrder.RejectionReason = stockRejected.Reason;
                    await _publisher.PublishAsync(Topics.OrderEvents, Subjects.OrderRejected,
                        new OrderRejected(rejectedOrder.Id, rejectedOrder.CustomerEmail, stockRejected.Reason), args.CancellationToken);
                    _logger.LogWarning("Pedido {OrderId} rechazado: {Reason}", rejectedOrder.Id, stockRejected.Reason);
                    break;

                default:
                    await args.DeadLetterMessageAsync(message, "UnexpectedSubject", $"Subject '{message.Subject}' no esperado");
                    return;
            }

            await args.CompleteMessageAsync(message, args.CancellationToken);
        }

        // Si la API se reinició, el pedido ya no está en memoria: reintentar no lo arreglaría,
        // así que se manda a la dead-letter queue para poder inspeccionarlo.
        private Task DeadLetterOrderNotFoundAsync(ProcessMessageEventArgs args, Guid orderId)
        {
            _logger.LogWarning("Pedido {OrderId} no encontrado, mensaje a dead-letter", orderId);
            return args.DeadLetterMessageAsync(args.Message, "OrderNotFound", $"No existe el pedido {orderId}");
        }

        private Task HandleErrorAsync(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Error en {EntityPath} ({ErrorSource})", args.EntityPath, args.ErrorSource);
            return Task.CompletedTask;
        }
    }
}
