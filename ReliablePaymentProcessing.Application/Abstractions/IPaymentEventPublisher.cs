using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Application.Abstractions;

public interface IPaymentEventPublisher
{
    Task PublishPaymentCreatedAsync(Payment payment, CancellationToken cancellationToken = default);
}
