namespace PaymentFlowCloud.Application.Payments;

public class CreatePaymentCommand
{
    public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    public string CorrelationId { get; set; } = default!;
}
