using PaymentFlowCloud.Domain.Entities;

namespace PaymentFlowCloud.Api.Contracts;

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
        // API 返回 DTO，避免直接暴露领域对象和 EF 导航属性。
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

