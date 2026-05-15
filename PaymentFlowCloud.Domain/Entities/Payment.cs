namespace PaymentFlowCloud.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }

    public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    public string Status { get; set; } = "Pending";

    public string TraceId { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
}