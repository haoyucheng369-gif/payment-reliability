namespace ReliablePaymentProcessing.Application.Payments;

public class DuplicateOrderPaymentException(Guid orderId) : Exception(
    $"Payment for order '{orderId}' already exists.")
{
    public Guid OrderId { get; } = orderId;
}

