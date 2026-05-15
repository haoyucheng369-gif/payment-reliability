using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Abstractions;

public interface IPaymentEventPublisher
{
    Task PublishPaymentCreatedAsync(Payment payment, CancellationToken cancellationToken = default);
}
