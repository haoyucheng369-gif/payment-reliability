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
        // Controller 只负责 HTTP DTO 到应用层命令的转换，不直接处理数据库或消息队列。
        var correlationId = Request.Headers.TryGetValue("X-Correlation-Id", out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue)
                ? headerValue.ToString()
                : Guid.NewGuid().ToString();

        var payment = await createPaymentService.CreateAsync(
            new CreatePaymentCommand
            {
                MerchantOrderId = request.MerchantOrderId,
                Amount = request.Amount,
                Currency = request.Currency,
                CorrelationId = correlationId
            },
            cancellationToken);

        return Ok(payment);
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
            : Ok(payment);
    }
}
