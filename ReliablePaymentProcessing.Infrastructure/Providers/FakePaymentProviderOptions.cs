namespace ReliablePaymentProcessing.Infrastructure.Providers;

                                                     public class FakePaymentProviderOptions
{
                                                                              public string BaseUrl { get; set; } = "http://localhost:5290";

                                                     public string WebhookUrl { get; set; } = "http://localhost:5147/webhooks/fake-provider/payment-succeeded";

                                                                                    public int TimeoutSeconds { get; set; } = 5;
}
