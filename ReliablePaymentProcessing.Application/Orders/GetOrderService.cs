using ReliablePaymentProcessing.Application.Abstractions;
using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Application.Orders;

public class GetOrderService(IOrderRepository orderRepository)
{
    public async Task<Order?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
                                                     return await orderRepository.FindByIdAsync(id, cancellationToken);
    }
}

