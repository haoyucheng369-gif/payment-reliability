using Microsoft.Extensions.Logging;
using ReliablePaymentProcessing.Application.Abstractions;

namespace ReliablePaymentProcessing.Application.Payments;

public class ProcessPaymentService(
    IPaymentRepository paymentRepository,
    ILogger<ProcessPaymentService> logger)
{
    public async Task<bool> MarkProcessingAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
                                                                   var payment = await paymentRepository.FindByIdAsync(paymentId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning("Payment {PaymentId} was not found during worker processing", paymentId);
            return false;
        }

        payment.MarkProcessing();

        await paymentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Payment {PaymentId} marked processing after fake provider accepted it",
            payment.Id);

        return true;
    }
}
