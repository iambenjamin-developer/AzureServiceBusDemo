namespace Shared.Contracts.Events;

public record OrderRejected(Guid OrderId, string CustomerEmail, string Reason);
