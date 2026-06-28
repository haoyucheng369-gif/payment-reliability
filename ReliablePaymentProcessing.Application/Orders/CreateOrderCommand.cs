namespace ReliablePaymentProcessing.Application.Orders;

public class CreateOrderCommand
{
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";
}

