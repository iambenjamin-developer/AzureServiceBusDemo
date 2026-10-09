namespace Shared.Contracts.Events;

public record OrderPlaced(Guid OrderId, string CustomerEmail, List<OrderItem> Items);
