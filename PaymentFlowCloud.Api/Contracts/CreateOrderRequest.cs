namespace PaymentFlowCloud.Api.Contracts;

public class CreateOrderRequest
{
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";
}

