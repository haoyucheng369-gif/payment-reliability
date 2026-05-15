namespace PaymentFlowCloud.Api.Contracts;

public class CreatePaymentRequest
{
    public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";
}
