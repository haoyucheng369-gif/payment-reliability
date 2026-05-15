using Microsoft.EntityFrameworkCore;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Infrastructure.Persistence;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>()
            .HasIndex(payment => payment.MerchantOrderId)
            .IsUnique();

        // 金额显式限制精度，避免 SQL Server 使用默认 decimal 精度导致截断风险。
        modelBuilder.Entity<Payment>()
            .Property(payment => payment.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(payment => payment.MerchantOrderId)
            .HasMaxLength(128);

        modelBuilder.Entity<Payment>()
            .Property(payment => payment.CorrelationId)
            .HasMaxLength(128);

        // C# 使用 enum，数据库保留可读字符串，方便排查和手工查询。
        modelBuilder.Entity<Payment>()
            .Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(32);
    }
}
