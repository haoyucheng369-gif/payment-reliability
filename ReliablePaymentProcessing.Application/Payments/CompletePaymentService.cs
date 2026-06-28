using Microsoft.Extensions.Logging;
using ReliablePaymentProcessing.Application.Abstractions;
using ReliablePaymentProcessing.Application.Common;
using ReliablePaymentProcessing.Application.Contracts;

namespace ReliablePaymentProcessing.Application.Payments;

public class CompletePaymentService(
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    ILogger<CompletePaymentService> logger)
{
    public async Task CompleteAsync(
        FakeProviderWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.FindByIdAsync(request.PaymentId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning(
                "Webhook ignored because payment {PaymentId} was not found",
                request.PaymentId);

            throw new NotFoundException("Payment", request.PaymentId);
        }

                                                              if (!string.Equals(request.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Webhook ignored unsupported provider status {ProviderStatus} for payment {PaymentId}",
                request.Status,
                request.PaymentId);

            return;
        }

        payment.MarkSucceeded();

        if (payment.OrderId is not null)
        {
            var order = await orderRepository.FindByIdAsync(payment.OrderId.Value, cancellationToken);
            order?.MarkPaid();
        }

        await paymentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Payment {PaymentId} completed by provider payment {ProviderPaymentId}; order {OrderId} marked paid",
            payment.Id,
            request.ProviderPaymentId,
            payment.OrderId);
    }
}
