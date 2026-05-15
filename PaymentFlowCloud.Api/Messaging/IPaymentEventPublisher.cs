using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Api.Messaging;

public interface IPaymentEventPublisher
{
    Task PublishPaymentCreatedAsync(Payment payment, CancellationToken cancellationToken = default);
}
