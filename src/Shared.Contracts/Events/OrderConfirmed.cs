namespace Shared.Contracts.Events;

public record OrderConfirmed(Guid OrderId, string CustomerEmail);
