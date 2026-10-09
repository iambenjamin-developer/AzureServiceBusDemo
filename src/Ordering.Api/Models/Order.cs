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
        public Guid Id { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public List<OrderLine> Items { get; set; } = new();
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public string? RejectionReason { get; set; }
    }

    public class OrderLine
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
