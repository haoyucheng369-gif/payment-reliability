using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.Application.Observability;
using PaymentFlowCloud.Application.Security;
using PaymentFlowCloud.ProviderMock;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    // ProviderMock 也写入 Seq，方便按 CorrelationId 查看 API -> Worker -> Provider -> Webhook。
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341");
});

builder.Services.AddHttpClient();
builder.Services.Configure<ProviderMockOptions>(
    builder.Configuration.GetSection("ProviderMock"));
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource.AddService("PaymentFlowCloud.ProviderMock");
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(PaymentFlowCloudTelemetry.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(
                    builder.Configuration["OpenTelemetry:OtlpEndpoint"]
                    ?? "http://localhost:4317");
            });

        var applicationInsightsConnectionString =
            builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? builder.Configuration["ApplicationInsights:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
        {
            // 配置了 Application Insights 连接字符串时，同一份 trace 会额外发送到 Azure Monitor。
            tracing.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = applicationInsightsConnectionString;
            });
        }
    });

var app = builder.Build();

app.MapPost("/provider/payments", async (
    FakeProviderPaymentRequest request,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    IOptions<ProviderMockOptions> options,
    CancellationToken cancellationToken) =>
{
    using var correlationScope = LogContext.PushProperty("CorrelationId", request.CorrelationId);
    using var paymentScope = LogContext.PushProperty("PaymentId", request.PaymentId);

    var logger = loggerFactory.CreateLogger("PaymentFlowCloud.ProviderMock");
    var providerOptions = options.Value;
    var providerPaymentId = $"fp_{Guid.NewGuid():N}";

    if (string.Equals(providerOptions.Mode, "Http500", StringComparison.OrdinalIgnoreCase))
    {
        // 模拟第三方支付平台同步故障，Worker 应该进入 retry / DLQ。
        logger.LogWarning(
            "Fake provider returning HTTP 500 for payment {PaymentId}",
            request.PaymentId);

        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    if (string.Equals(providerOptions.Mode, "Timeout", StringComparison.OrdinalIgnoreCase))
    {
        // 模拟第三方支付平台长时间不响应，Worker HttpClient 超时后应该进入 retry / DLQ。
        logger.LogWarning(
            "Fake provider delaying response for payment {PaymentId} to simulate timeout",
            request.PaymentId);

        await Task.Delay(
            TimeSpan.FromSeconds(Math.Max(1, providerOptions.TimeoutDelaySeconds)),
            cancellationToken);

        return Results.Ok(new FakeProviderPaymentResponse
        {
            ProviderPaymentId = providerPaymentId,
            Status = "Accepted"
        });
    }

    logger.LogInformation(
        "Fake provider accepted payment {PaymentId} with provider payment {ProviderPaymentId}",
        request.PaymentId,
        providerPaymentId);

    _ = Task.Run(async () =>
    {
        using var callbackCorrelationScope = LogContext.PushProperty("CorrelationId", request.CorrelationId);
        using var callbackPaymentScope = LogContext.PushProperty("PaymentId", request.PaymentId);
        using var webhookActivity = PaymentFlowCloudTelemetry.ActivitySource.StartActivity(
            "provider webhook delivery",
            ActivityKind.Internal);
        webhookActivity?.SetTag("payment.id", request.PaymentId);
        webhookActivity?.SetTag("correlation.id", request.CorrelationId);

        try
        {
            // 模拟第三方支付平台异步处理后主动回调商户 webhook。
            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(1, providerOptions.WebhookDelaySeconds)),
                CancellationToken.None);

            var webhook = new FakeProviderWebhookRequest
            {
                PaymentId = request.PaymentId,
                ProviderPaymentId = providerPaymentId,
                Status = "Succeeded",
                CorrelationId = request.CorrelationId
            };

            var rawBody = JsonSerializer.Serialize(
                webhook,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            var client = httpClientFactory.CreateClient();
            await SendWebhookWithRetryAsync(
                client,
                request.WebhookUrl,
                request.CorrelationId,
                request.PaymentId,
                providerOptions,
                rawBody,
                logger);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Fake provider stopped webhook delivery for payment {PaymentId}",
                request.PaymentId);
        }
    }, CancellationToken.None);

    return Results.Ok(new FakeProviderPaymentResponse
    {
        ProviderPaymentId = providerPaymentId,
        Status = "Accepted"
    });
});

app.Run();

static async Task SendWebhookWithRetryAsync(
    HttpClient client,
    string webhookUrl,
    string correlationId,
    Guid paymentId,
    ProviderMockOptions providerOptions,
    string rawBody,
    Microsoft.Extensions.Logging.ILogger logger)
{
    var maxAttempts = Math.Max(1, providerOptions.WebhookMaxRetryCount);
    var baseDelaySeconds = Math.Max(1, providerOptions.WebhookRetryBaseDelaySeconds);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var httpRequest = CreateSignedWebhookRequest(
                webhookUrl,
                correlationId,
                providerOptions.WebhookSecret,
                rawBody);

            using var response = await client.SendAsync(httpRequest, CancellationToken.None);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Fake provider sent signed succeeded webhook for payment {PaymentId} on attempt {WebhookAttempt}",
                    paymentId,
                    attempt);
                return;
            }

            if (!ShouldRetry(response.StatusCode))
            {
                logger.LogWarning(
                    "Fake provider webhook for payment {PaymentId} returned non-retryable status {StatusCode} on attempt {WebhookAttempt}",
                    paymentId,
                    (int)response.StatusCode,
                    attempt);
                return;
            }

            logger.LogWarning(
                "Fake provider webhook for payment {PaymentId} returned retryable status {StatusCode} on attempt {WebhookAttempt}",
                paymentId,
                (int)response.StatusCode,
                attempt);
        }
        catch (Exception ex) when (IsRetryableWebhookException(ex))
        {
            logger.LogWarning(
                ex,
                "Fake provider webhook for payment {PaymentId} failed on attempt {WebhookAttempt}",
                paymentId,
                attempt);
        }

        if (attempt < maxAttempts)
        {
            // 简单递增退避：第 1 次失败等 1s，第 2 次失败等 2s，便于本地观察。
            await Task.Delay(TimeSpan.FromSeconds(baseDelaySeconds * attempt), CancellationToken.None);
        }
    }

    logger.LogError(
        "Fake provider webhook delivery exhausted {WebhookMaxAttempts} attempts for payment {PaymentId}",
        maxAttempts,
        paymentId);
}

static HttpRequestMessage CreateSignedWebhookRequest(
    string webhookUrl,
    string correlationId,
    string webhookSecret,
    string rawBody)
{
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var signature = FakeProviderWebhookSignature.Create(
        webhookSecret,
        timestamp,
        rawBody);

    var httpRequest = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
    {
        Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
    };
    httpRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
    httpRequest.Headers.TryAddWithoutValidation(
        FakeProviderWebhookSignature.TimestampHeaderName,
        timestamp.ToString());
    httpRequest.Headers.TryAddWithoutValidation(
        FakeProviderWebhookSignature.SignatureHeaderName,
        signature);

    return httpRequest;
}

static bool ShouldRetry(HttpStatusCode statusCode)
{
    return (int)statusCode >= StatusCodes.Status500InternalServerError
        || statusCode == HttpStatusCode.RequestTimeout
        || statusCode == HttpStatusCode.TooManyRequests;
}

static bool IsRetryableWebhookException(Exception exception)
{
    return exception is HttpRequestException or TaskCanceledException or TimeoutException;
}
