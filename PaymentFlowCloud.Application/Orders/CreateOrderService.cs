using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Orders;

public class CreateOrderService(IOrderRepository orderRepository)
{
    public async Task<Order> CreateAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        // 真实系统里订单号通常由后端生成；前端刷新页面不应该自己生成新订单。
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

