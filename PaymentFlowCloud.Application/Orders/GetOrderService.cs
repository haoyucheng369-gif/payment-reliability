using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Application.Orders;

public class GetOrderService(IOrderRepository orderRepository)
{
    public async Task<Order?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // 查询订单用于刷新页面后恢复同一个订单上下文。
        return await orderRepository.FindByIdAsync(id, cancellationToken);
    }
}

