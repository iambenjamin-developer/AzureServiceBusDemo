using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;

namespace Shared.Messaging;

public class ServiceBusEventPublisher : IEventPublisher
{
    private readonly ServiceBusClient _client;

    // Los senders son thread-safe y costosos de crear: se crea uno por topic y se reutiliza.
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public ServiceBusEventPublisher(ServiceBusClient client)
    {
        _client = client;
    }

    public async Task PublishAsync<T>(string topic, string subject, T @event, CancellationToken cancellationToken = default)
    {
        var sender = _senders.GetOrAdd(topic, _client.CreateSender);

        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(@event))
        {
            MessageId = Guid.NewGuid().ToString(),
            Subject = subject,
            ContentType = "application/json"
        };

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
