using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Api.Contracts;

public class OrderResponse
{
    public Guid Id { get; set; }

    public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    public string Status { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public static OrderResponse From(Order order)
    {
                                                             return new OrderResponse
        {
            Id = order.Id,
            MerchantOrderId = order.MerchantOrderId,
            Amount = order.Amount,
            Currency = order.Currency,
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt
        };
    }
}

