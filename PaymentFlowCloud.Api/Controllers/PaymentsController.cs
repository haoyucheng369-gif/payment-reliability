using Microsoft.AspNetCore.Mvc;
using PaymentFlowCloud.Api.Contracts;
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
        // 每次 HTTP 请求可以有新的 CorrelationId，但同一个 OrderId 重复请求只会返回同一笔 Payment。
        var correlationId = Request.Headers.TryGetValue("X-Correlation-Id", out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue)
                ? headerValue.ToString()
                : Guid.NewGuid().ToString();

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
