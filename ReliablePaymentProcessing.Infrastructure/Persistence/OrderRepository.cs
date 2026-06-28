using Microsoft.EntityFrameworkCore;
using ReliablePaymentProcessing.Application.Abstractions;
using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Infrastructure.Persistence;

public class OrderRepository(PaymentDbContext dbContext) : IOrderRepository
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
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

