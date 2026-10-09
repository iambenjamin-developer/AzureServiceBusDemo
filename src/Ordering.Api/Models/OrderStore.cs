using System.Collections.Concurrent;

namespace Ordering.Api.Models
{
    /// <summary>
    /// "Base de datos" en memoria. Se pierde al reiniciar la API (suficiente para la demo).
    /// </summary>
    public class OrderStore
    {
        private readonly ConcurrentDictionary<Guid, Order> _orders = new();

        public void Add(Order order) => _orders[order.Id] = order;

        public Order? Get(Guid id) => _orders.GetValueOrDefault(id);
    }
}
