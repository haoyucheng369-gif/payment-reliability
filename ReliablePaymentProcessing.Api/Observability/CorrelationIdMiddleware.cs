using System.Diagnostics;

namespace ReliablePaymentProcessing.Api.Observability;

public class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public const string ItemName = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[ItemName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

                                                                     using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        logger.LogInformation(
            "HTTP request started {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        await next(context);

        logger.LogInformation(
            "HTTP request completed {Method} {Path} with status {StatusCode}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
