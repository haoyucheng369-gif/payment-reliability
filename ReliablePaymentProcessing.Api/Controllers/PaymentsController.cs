using Microsoft.AspNetCore.Mvc;
using ReliablePaymentProcessing.Api.Contracts;
using ReliablePaymentProcessing.Api.Observability;
using ReliablePaymentProcessing.Application.Payments;

namespace ReliablePaymentProcessing.Api.Controllers;

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
                                                                     var payment = await getPaymentService.GetByIdAsync(id, cancellationToken);

        return payment is null
            ? NotFound()
            : Ok(PaymentResponse.From(payment));
    }
}
