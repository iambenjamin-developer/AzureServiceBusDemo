using System.ComponentModel.DataAnnotations;
using Shared.Contracts.Events;

namespace Ordering.Api.Models
{
    public class PlaceOrderRequest
    {
        [Required, EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required, MinLength(1)]
        public List<OrderItem> Items { get; set; } = new();
    }
}
