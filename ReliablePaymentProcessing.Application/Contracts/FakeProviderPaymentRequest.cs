namespace ReliablePaymentProcessing.Application.Contracts;

                                          public class FakeProviderPaymentRequest
{
                                                           public Guid PaymentId { get; set; }

                                                         public Guid? OrderId { get; set; }

                                             public string MerchantOrderId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

                                                    public string CorrelationId { get; set; } = default!;

                                                           public string WebhookUrl { get; set; } = default!;
}
