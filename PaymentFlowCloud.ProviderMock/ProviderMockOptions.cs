namespace PaymentFlowCloud.ProviderMock;

public class ProviderMockOptions
{
    // Success: 正常 accepted + webhook；Http500: 同步返回 500；Timeout: 延迟到 Worker 超时。
    public string Mode { get; set; } = "Success";

    // Provider webhook 共享密钥，用来生成 HMAC 签名。
    public string WebhookSecret { get; set; } = "local-dev-provider-webhook-secret";

    // 正常成功模式下，provider 延迟几秒后回调 webhook。
    public int WebhookDelaySeconds { get; set; } = 3;

    // Webhook 投递最多尝试次数，模拟真实支付平台 webhook 失败后的自动重试。
    public int WebhookMaxRetryCount { get; set; } = 3;

    // Webhook 每次重试的基础等待秒数，实际等待会按尝试次数递增。
    public int WebhookRetryBaseDelaySeconds { get; set; } = 1;

    // Timeout 模式下，provider 延迟几秒才返回，用来触发 Worker HttpClient timeout。
    public int TimeoutDelaySeconds { get; set; } = 10;
}
