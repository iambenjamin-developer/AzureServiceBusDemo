namespace Shared.Messaging;

public interface IEventPublisher
{
    /// <summary>
    /// Publica un evento en un topic. El "subject" es lo que usan los filtros
    /// de las subscriptions para decidir quién recibe el mensaje.
    /// </summary>
    Task PublishAsync<T>(string topic, string subject, T @event, CancellationToken cancellationToken = default);
}
