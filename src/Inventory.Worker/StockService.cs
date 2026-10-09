using Inventory.Worker.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Events;

namespace Inventory.Worker
{
    public class StockService
    {
        private readonly InventoryDbContext _db;

        public StockService(InventoryDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Reserva todos los items o ninguno.
        /// Devuelve null si se reservó, o el motivo del rechazo si falta stock.
        /// </summary>
        public async Task<string?> TryReserveAsync(IEnumerable<OrderItem> items, CancellationToken cancellationToken)
        {
            // Si el mismo producto viene en varias líneas, se suman las cantidades.
            var requested = items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

            var products = await _db.Products
                .Where(p => requested.Keys.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var (productId, quantity) in requested)
            {
                if (!products.TryGetValue(productId, out var product))
                {
                    return $"El producto {productId} no existe";
                }

                if (product.Stock < quantity)
                {
                    return $"Stock insuficiente para el producto {productId} (disponible: {product.Stock}, pedido: {quantity})";
                }
            }

            foreach (var (productId, quantity) in requested)
            {
                products[productId].Stock -= quantity;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }
    }
}
