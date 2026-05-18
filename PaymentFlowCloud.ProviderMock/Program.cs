using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.ProviderMock;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Microsoft.Extensions.Options;

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
        // 模拟第三方支付平台同步故障，Worker 应该走 retry / DLQ。
        logger.LogWarning(
            "Fake provider returning HTTP 500 for payment {PaymentId}",
            request.PaymentId);

        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    if (string.Equals(providerOptions.Mode, "Timeout", StringComparison.OrdinalIgnoreCase))
    {
        // 模拟第三方支付平台长时间不响应，Worker HttpClient 超时后应走 retry / DLQ。
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
        try
        {
            using var callbackCorrelationScope = LogContext.PushProperty("CorrelationId", request.CorrelationId);
            using var callbackPaymentScope = LogContext.PushProperty("PaymentId", request.PaymentId);

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

            var client = httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, request.WebhookUrl)
            {
                Content = JsonContent.Create(webhook)
            };
            httpRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);

            using var response = await client.SendAsync(httpRequest, CancellationToken.None);
            response.EnsureSuccessStatusCode();

            logger.LogInformation(
                "Fake provider sent succeeded webhook for payment {PaymentId}",
                request.PaymentId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Fake provider failed to send webhook for payment {PaymentId}",
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
