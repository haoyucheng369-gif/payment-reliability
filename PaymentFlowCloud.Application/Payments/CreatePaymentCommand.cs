namespace PaymentFlowCloud.Application.Payments;

public class CreatePaymentCommand
{
    public Guid OrderId { get; set; }

    public string CorrelationId { get; set; } = default!;
}

