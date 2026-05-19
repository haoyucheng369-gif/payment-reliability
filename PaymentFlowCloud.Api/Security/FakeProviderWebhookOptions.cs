namespace PaymentFlowCloud.Api.Security;

public class FakeProviderWebhookOptions
{
    public string Secret { get; set; } = default!;

    public int TimestampToleranceSeconds { get; set; } = 300;
}
