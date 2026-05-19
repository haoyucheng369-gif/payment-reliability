namespace PaymentFlowCloud.Application.Contracts;

// Fake provider 主动回调 API 时发送的支付结果。
public class FakeProviderWebhookRequest
{
    // 回调必须带回本系统 PaymentId，API 才能定位并更新支付记录。
    public Guid PaymentId { get; set; }

    // 第三方支付平台自己的支付流水号，用于日志和后续对账。
    public string ProviderPaymentId { get; set; } = default!;

    // 第一版只处理 Succeeded，后续可以扩展 Failed、Cancelled。
    public string Status { get; set; } = default!;

    // 沿用原请求链路 ID，方便在 Seq 里查完整异步链路。
    public string CorrelationId { get; set; } = default!;
}
