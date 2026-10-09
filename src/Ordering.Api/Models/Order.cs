using Shared.Contracts.Events;

namespace Ordering.Api.Models
{
    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Rejected
    }

    public class Order
    {
        public Guid Id { get; init; }
        public string CustomerEmail { get; init; } = string.Empty;
        public List<OrderItem> Items { get; init; } = new();
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public string? RejectionReason { get; set; }
    }
}
