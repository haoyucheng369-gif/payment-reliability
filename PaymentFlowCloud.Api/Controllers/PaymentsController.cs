using Microsoft.AspNetCore.Mvc;
using PaymentFlowCloud.Api.Contracts;
using PaymentFlowCloud.Api.Observability;
using PaymentFlowCloud.Application.Payments;

namespace PaymentFlowCloud.Api.Controllers;

[ApiController]
[Route("payments")]
public class PaymentsController(
    CreatePaymentService createPaymentService,
    GetPaymentService getPaymentService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        // CorrelationId 由 middleware 统一解析，支付请求只负责传入应用层命令。
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.ItemName] as string
            ?? Guid.NewGuid().ToString();

        var payment = await createPaymentService.CreateAsync(
            new CreatePaymentCommand
            {
                OrderId = request.OrderId,
                CorrelationId = correlationId
            },
            cancellationToken);

        return Ok(PaymentResponse.From(payment));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        // 查询接口用于观察异步处理后的状态变化，避免调试时必须手动查 SQL。
        var payment = await getPaymentService.GetByIdAsync(id, cancellationToken);

        return payment is null
            ? NotFound()
            : Ok(PaymentResponse.From(payment));
    }
}
