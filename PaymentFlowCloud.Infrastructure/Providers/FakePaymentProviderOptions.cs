namespace PaymentFlowCloud.Infrastructure.Providers;

// fake provider 的地址配置，API 和 Worker 可按运行环境覆盖。
public class FakePaymentProviderOptions
{
    // Worker 调用 provider 的基础地址；Docker 内使用服务名，本机调试使用 localhost。
    public string BaseUrl { get; set; } = "http://localhost:5290";

    // provider 异步处理完成后回调 API 的 webhook 地址。
    public string WebhookUrl { get; set; } = "http://localhost:5147/webhooks/fake-provider/payment-succeeded";

    // Worker 调用 provider 的超时时间；用于验证 provider 迟迟不返回时的 retry / DLQ 行为。
    public int TimeoutSeconds { get; set; } = 5;
}
