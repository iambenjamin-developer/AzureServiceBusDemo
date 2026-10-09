using Microsoft.AspNetCore.Mvc;
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
        private readonly OrderStore _store;
        private readonly IEventPublisher _publisher;

        public OrdersController(OrderStore store, IEventPublisher publisher)
        {
            _store = store;
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
            };
            _store.Add(order);

            await _publisher.PublishAsync(Topics.OrderEvents, Subjects.OrderPlaced,
                new OrderPlaced(order.Id, order.CustomerEmail, order.Items), cancellationToken);

            // 202 Accepted: el pedido se procesa de forma asíncrona.
            // El cliente consulta el estado en la URL del header Location.
            return AcceptedAtAction(nameof(GetById), new { id = order.Id }, order);
        }

        // GET api/orders/{id}
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetById(Guid id)
        {
            var order = _store.Get(id);
            return order is null ? NotFound() : Ok(order);
        }
    }
}
