using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Abstractions;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task<Order?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Order?> FindByMerchantOrderIdAsync(
        string merchantOrderId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

