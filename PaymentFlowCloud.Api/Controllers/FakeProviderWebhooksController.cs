using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PaymentFlowCloud.Api.Security;
using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.Application.Payments;
using PaymentFlowCloud.Application.Security;

namespace PaymentFlowCloud.Api.Controllers;

[ApiController]
[Route("webhooks/fake-provider")]
public class FakeProviderWebhooksController(
    CompletePaymentService completePaymentService,
    IOptions<FakeProviderWebhookOptions> webhookOptions,
    ILogger<FakeProviderWebhooksController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions WebhookJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpPost("payment-succeeded")]
    public async Task<IActionResult> PaymentSucceeded(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        if (!IsSignatureValid(rawBody))
        {
            return Unauthorized(new { status = "invalid_signature" });
        }

        var request = JsonSerializer.Deserialize<FakeProviderWebhookRequest>(
            rawBody,
            WebhookJsonOptions);

        if (request is null)
        {
            return BadRequest(new { status = "invalid_payload" });
        }

        // 第一版 webhook 只模拟支付成功；重复成功回调由应用层和领域状态保持幂等。
        await completePaymentService.CompleteAsync(request, cancellationToken);

        return Ok(new { status = "received" });
    }

    private bool IsSignatureValid(string rawBody)
    {
        var options = webhookOptions.Value;

        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            logger.LogError("Fake provider webhook secret is not configured");
            return false;
        }

        if (!Request.Headers.TryGetValue(FakeProviderWebhookSignature.TimestampHeaderName, out var timestampHeader)
            || !long.TryParse(timestampHeader.ToString(), out var unixTimestampSeconds))
        {
            logger.LogWarning("Rejected fake provider webhook because timestamp header is missing or invalid");
            return false;
        }

        if (!Request.Headers.TryGetValue(FakeProviderWebhookSignature.SignatureHeaderName, out var signatureHeader))
        {
            logger.LogWarning("Rejected fake provider webhook because signature header is missing");
            return false;
        }

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestampSeconds);
        var age = (DateTimeOffset.UtcNow - timestamp).Duration();
        if (age > TimeSpan.FromSeconds(options.TimestampToleranceSeconds))
        {
            logger.LogWarning(
                "Rejected fake provider webhook because timestamp age {TimestampAgeSeconds}s exceeds tolerance {ToleranceSeconds}s",
                age.TotalSeconds,
                options.TimestampToleranceSeconds);
            return false;
        }

        var isValid = FakeProviderWebhookSignature.Verify(
            options.Secret,
            unixTimestampSeconds,
            rawBody,
            signatureHeader.ToString());

        if (!isValid)
        {
            logger.LogWarning("Rejected fake provider webhook because signature verification failed");
        }

        return isValid;
    }
}
