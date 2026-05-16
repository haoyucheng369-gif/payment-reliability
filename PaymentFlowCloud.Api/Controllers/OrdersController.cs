using Microsoft.AspNetCore.Mvc;
using PaymentFlowCloud.Api.Contracts;
using PaymentFlowCloud.Application.Orders;

namespace PaymentFlowCloud.Api.Controllers;

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
        // Controller 只负责 HTTP DTO 到应用层命令的转换，订单号由后端生成。
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
        // 查询订单用于前端刷新后恢复订单上下文。
        var order = await getOrderService.GetByIdAsync(id, cancellationToken);

        return order is null
            ? NotFound()
            : Ok(OrderResponse.From(order));
    }
}

