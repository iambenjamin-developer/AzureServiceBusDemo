using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Data;
using Ordering.Api.Models;
using Shared.Contracts;
using Shared.Contracts.Events;
using Shared.Messaging;

namespace Ordering.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly OrderingDbContext _db;
        private readonly IEventPublisher _publisher;

        public OrdersController(OrderingDbContext db, IEventPublisher publisher)
        {
            _db = db;
            _publisher = publisher;
        }

        // POST api/orders
        [HttpPost]
        [ProducesResponseType(typeof(Order), StatusCodes.Status202Accepted)]
        public async Task<IActionResult> Place(PlaceOrderRequest request, CancellationToken cancellationToken)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerEmail = request.CustomerEmail,
                Items = request.Items
                    .Select(i => new OrderLine { ProductId = i.ProductId, Quantity = i.Quantity })
                    .ToList()
            };

            // 1. Guardar el pedido en la base de datos (estado Pending).
            _db.Orders.Add(order);
            await _db.SaveChangesAsync(cancellationToken);

            // 2. Publicar el evento para que Inventory reserve el stock.
            await _publisher.PublishAsync(Topics.OrderEvents, Subjects.OrderPlaced,
                new OrderPlaced(order.Id, order.CustomerEmail, request.Items), cancellationToken);

            // 202 Accepted: el pedido se procesa de forma asíncrona.
            // El cliente consulta el estado en la URL del header Location.
            return AcceptedAtAction(nameof(GetById), new { id = order.Id }, order);
        }

        // GET api/orders/{id}
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

            return order is null ? NotFound() : Ok(order);
        }
    }
}
