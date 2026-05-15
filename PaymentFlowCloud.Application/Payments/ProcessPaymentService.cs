using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Payments;

public class ProcessPaymentService(IPaymentRepository paymentRepository)
{
    public async Task<bool> MarkProcessedAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.FindByIdAsync(paymentId, cancellationToken);

        if (payment is null)
        {
            return false;
        }

        payment.MarkProcessed();
        await paymentRepository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
