namespace PaymentFlowCloud.Application.Payments;

public class DuplicateMerchantOrderException(string merchantOrderId) : Exception
{
    public string MerchantOrderId { get; } = merchantOrderId;
}
