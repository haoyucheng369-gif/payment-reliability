using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Api.Contracts;

public class PaymentResponse
{
    public Guid Id { get; set; }

    public Guid? OrderId { get; set; }

    public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    public string Status { get; set; } = default!;

    public string CorrelationId { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public static PaymentResponse From(Payment payment)
    {
                                                                return new PaymentResponse
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            MerchantOrderId = payment.MerchantOrderId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status.ToString(),
            CorrelationId = payment.CorrelationId,
            CreatedAt = payment.CreatedAt
        };
    }
}

