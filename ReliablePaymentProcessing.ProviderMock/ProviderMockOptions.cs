namespace ReliablePaymentProcessing.ProviderMock;

public class ProviderMockOptions
{
                                                                                         public string Mode { get; set; } = "Success";

                                                    public string WebhookSecret { get; set; } = "local-dev-provider-webhook-secret";

                                                    public int WebhookDelaySeconds { get; set; } = 3;

                                                                    public int WebhookMaxRetryCount { get; set; } = 3;

                                                          public int WebhookRetryBaseDelaySeconds { get; set; } = 1;

                                                                                public int TimeoutDelaySeconds { get; set; } = 10;
}
