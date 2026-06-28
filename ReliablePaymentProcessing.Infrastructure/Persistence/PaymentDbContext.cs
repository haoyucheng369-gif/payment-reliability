using Microsoft.EntityFrameworkCore;
using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Infrastructure.Persistence;

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
            .HasIndex(order => new { order.Status, order.CreatedAt });

        modelBuilder.Entity<Order>()
            .Property(order => order.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(order => order.MerchantOrderId)
            .HasMaxLength(128);

        modelBuilder.Entity<Order>()
            .Property(order => order.Currency)
            .HasMaxLength(3);

                                                                modelBuilder.Entity<Order>()
            .Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(32);
    }

    private static void ConfigurePayment(ModelBuilder modelBuilder)
    {
                                                                 modelBuilder.Entity<Payment>()
            .HasIndex(payment => payment.OrderId)
            .IsUnique()
            .HasFilter("[OrderId] IS NOT NULL");

                                                                                   modelBuilder.Entity<Payment>()
            .HasIndex(payment => new { payment.Status, payment.CreatedAt });

        modelBuilder.Entity<Payment>()
            .HasOne(payment => payment.Order)
            .WithMany()
            .HasForeignKey(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

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

                                                                modelBuilder.Entity<Payment>()
            .Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(32);
    }
}
