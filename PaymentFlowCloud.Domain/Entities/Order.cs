namespace PaymentFlowCloud.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }

    public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    public OrderStatus Status { get; private set; } = OrderStatus.PendingPayment;

    public DateTime CreatedAt { get; set; }

    public void MarkPaid()
    {
        // 支付处理成功后，订单从待支付流转到已支付。
        if (Status == OrderStatus.PendingPayment)
        {
            Status = OrderStatus.Paid;
        }
    }
}

