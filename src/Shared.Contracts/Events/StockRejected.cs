namespace Shared.Contracts.Events;

public record StockRejected(Guid OrderId, string Reason);
