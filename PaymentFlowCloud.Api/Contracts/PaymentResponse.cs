using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Api.Contracts;

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
        // 手写映射比当前阶段引入 AutoMapper 更直接，也更容易调试。
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

