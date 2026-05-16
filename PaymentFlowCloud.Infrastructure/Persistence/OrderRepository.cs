using Microsoft.EntityFrameworkCore;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Infrastructure.Persistence;

public class OrderRepository(PaymentDbContext dbContext) : IOrderRepository
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        // 仓储只封装持久化入口，不承载业务规则。
        await dbContext.Orders.AddAsync(order, cancellationToken);
    }

    public async Task<Order?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public async Task<Order?> FindByMerchantOrderIdAsync(
        string merchantOrderId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .SingleOrDefaultAsync(
                order => order.MerchantOrderId == merchantOrderId,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

