using ReliablePaymentProcessing.Application.Abstractions;
using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Application.Orders;

public class CreateOrderService(IOrderRepository orderRepository)
{
    public async Task<Order> CreateAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
                                                                     var order = new Order
        {
            Id = Guid.NewGuid(),
            MerchantOrderId = $"ORDER-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}"[..32],
            Amount = command.Amount,
            Currency = command.Currency,
            CreatedAt = DateTime.UtcNow
        };

        await orderRepository.AddAsync(order, cancellationToken);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return order;
    }
}

