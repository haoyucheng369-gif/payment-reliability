using Microsoft.EntityFrameworkCore;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Infrastructure.Persistence;

public class PaymentRepository(PaymentDbContext dbContext) : IPaymentRepository
{
    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await dbContext.Payments.AddAsync(payment, cancellationToken);
    }

    public async Task<Payment?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .SingleOrDefaultAsync(payment => payment.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
