using System.Diagnostics;

namespace ReliablePaymentProcessing.Application.Observability;

public static class ReliablePaymentProcessingTelemetry
{
                                                                           public const string ActivitySourceName = "ReliablePaymentProcessing";

    public const string TraceParentHeaderName = "traceparent";

    public const string TraceStateHeaderName = "tracestate";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
