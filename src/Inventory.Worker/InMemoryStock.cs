using Shared.Contracts.Events;

namespace Inventory.Worker
{
    /// <summary>
    /// Stock falso en memoria (ProductId -> cantidad). Se reinicia cada vez que arranca el worker.
    /// </summary>
    public class InMemoryStock
    {
        private readonly Dictionary<int, int> _stock = new()
        {
            [1] = 10,
            [2] = 5,
            [3] = 0
        };

        /// <summary>
        /// Reserva todos los items o ninguno. Devuelve false y el motivo si falta stock.
        /// </summary>
        public bool TryReserve(IEnumerable<OrderItem> items, out string reason)
        {
            foreach (var item in items)
            {
                var available = _stock.GetValueOrDefault(item.ProductId);
                if (available < item.Quantity)
                {
                    reason = $"Stock insuficiente para el producto {item.ProductId} (disponible: {available}, pedido: {item.Quantity})";
                    return false;
                }
            }

            foreach (var item in items)
            {
                _stock[item.ProductId] -= item.Quantity;
            }

            reason = string.Empty;
            return true;
        }
    }
}
