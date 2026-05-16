using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Abstractions;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    Task<Payment?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Payment?> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

