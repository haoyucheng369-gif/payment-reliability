using Microsoft.EntityFrameworkCore;
using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Infrastructure.Persistence;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureOrder(modelBuilder);
        ConfigurePayment(modelBuilder);
    }

    private static void ConfigureOrder(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .HasIndex(order => order.MerchantOrderId)
            .IsUnique();

        modelBuilder.Entity<Order>()
            .Property(order => order.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(order => order.MerchantOrderId)
            .HasMaxLength(128);

        modelBuilder.Entity<Order>()
            .Property(order => order.Currency)
            .HasMaxLength(3);

        // C# 使用 enum，数据库保留可读字符串，方便排查和手工查询。
        modelBuilder.Entity<Order>()
            .Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(32);
    }

    private static void ConfigurePayment(ModelBuilder modelBuilder)
    {
        // 支付幂等以 OrderId 为准：同一个订单只能创建一条支付记录。
        modelBuilder.Entity<Payment>()
            .HasIndex(payment => payment.OrderId)
            .IsUnique()
            .HasFilter("[OrderId] IS NOT NULL");

        modelBuilder.Entity<Payment>()
            .HasOne(payment => payment.Order)
            .WithMany()
            .HasForeignKey(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // 金额显式限制精度，避免 SQL Server 使用默认 decimal 精度导致截断风险。
        modelBuilder.Entity<Payment>()
            .Property(payment => payment.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(payment => payment.MerchantOrderId)
            .HasMaxLength(128);

        modelBuilder.Entity<Payment>()
            .Property(payment => payment.Currency)
            .HasMaxLength(3);

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
