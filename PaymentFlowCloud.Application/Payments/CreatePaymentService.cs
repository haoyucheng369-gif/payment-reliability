using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Payments;

public class CreatePaymentService(
    IPaymentRepository paymentRepository,
    IPaymentEventPublisher paymentEventPublisher)
{
    public async Task<Payment> CreateAsync(
        CreatePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantOrderId = command.MerchantOrderId,
            Amount = command.Amount,
            Currency = command.Currency,
            TraceId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        };

        await paymentRepository.AddAsync(payment, cancellationToken);
        await paymentRepository.SaveChangesAsync(cancellationToken);

        await paymentEventPublisher.PublishPaymentCreatedAsync(payment, cancellationToken);

        return payment;
    }
}
