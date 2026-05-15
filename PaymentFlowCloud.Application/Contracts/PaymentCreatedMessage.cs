namespace PaymentFlowCloud.Application.Contracts;

public class PaymentCreatedMessage
{
    public Guid PaymentId { get; set; }

    public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    public string CorrelationId { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
}
