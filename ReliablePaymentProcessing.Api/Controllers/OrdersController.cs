using Microsoft.AspNetCore.Mvc;
using ReliablePaymentProcessing.Api.Contracts;
using ReliablePaymentProcessing.Application.Orders;

namespace ReliablePaymentProcessing.Api.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(
    CreateOrderService createOrderService,
    GetOrderService getOrderService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
                                                                         var order = await createOrderService.CreateAsync(
            new CreateOrderCommand
            {
                Amount = request.Amount,
                Currency = request.Currency
            },
            cancellationToken);

        return Ok(OrderResponse.From(order));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
                                                var order = await getOrderService.GetByIdAsync(id, cancellationToken);

        return order is null
            ? NotFound()
            : Ok(OrderResponse.From(order));
    }
}
