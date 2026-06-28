namespace ReliablePaymentProcessing.Application.Contracts;

                                          public class FakeProviderWebhookRequest
{
                                                          public Guid PaymentId { get; set; }

                                                   public string ProviderPaymentId { get; set; } = default!;

                                                           public string Status { get; set; } = default!;

                                                 public string CorrelationId { get; set; } = default!;
}
