using System.Diagnostics;

namespace PaymentFlowCloud.Application.Observability;

public static class PaymentFlowCloudTelemetry
{
    // 所有项目共享同一个 ActivitySource，保证自定义 span 可以被统一采集到 Tempo。
    public const string ActivitySourceName = "PaymentFlowCloud";

    public const string TraceParentHeaderName = "traceparent";

    public const string TraceStateHeaderName = "tracestate";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
