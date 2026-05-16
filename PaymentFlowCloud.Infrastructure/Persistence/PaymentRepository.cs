using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Application.Payments;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Infrastructure.Persistence;

public class PaymentRepository(PaymentDbContext dbContext) : IPaymentRepository
{
    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        // 仓储只封装持久化入口，不承载业务规则。
        await dbContext.Payments.AddAsync(payment, cancellationToken);
    }

    public async Task<Payment?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .SingleOrDefaultAsync(payment => payment.Id == id, cancellationToken);
    }

    public async Task<Payment?> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .SingleOrDefaultAsync(payment => payment.OrderId == orderId, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            var orderId = dbContext.ChangeTracker
                .Entries<Payment>()
                .Select(entry => entry.Entity.OrderId)
                .FirstOrDefault(orderId => orderId is not null);

            // SaveChanges 失败后移除本次新增实体，避免后续查询被本地跟踪实体干扰。
            foreach (var entry in dbContext.ChangeTracker.Entries<Payment>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.State = EntityState.Detached;
                }
            }

            if (orderId is not null)
            {
                throw new DuplicateOrderPaymentException(orderId.Value);
            }

            throw;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors
                .Cast<SqlError>()
                .Any(error => error.Number is 2601 or 2627);
    }
}
