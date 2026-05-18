namespace PaymentFlowCloud.ProviderMock;

public class ProviderMockOptions
{
    // Success: 正常 accepted + webhook；Http500: 同步返回 500；Timeout: 延迟到 Worker 超时。
    public string Mode { get; set; } = "Success";

    // 正常成功模式下，provider 延迟几秒后回调 webhook。
    public int WebhookDelaySeconds { get; set; } = 3;

    // Timeout 模式下，provider 延迟几秒才返回，用来触发 Worker HttpClient timeout。
    public int TimeoutDelaySeconds { get; set; } = 10;
}
