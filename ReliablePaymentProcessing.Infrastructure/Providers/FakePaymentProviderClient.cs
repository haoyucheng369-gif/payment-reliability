using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReliablePaymentProcessing.Application.Abstractions;
using ReliablePaymentProcessing.Application.Contracts;
using ReliablePaymentProcessing.Domain.Entities;

namespace ReliablePaymentProcessing.Infrastructure.Providers;

public class FakePaymentProviderClient(
    HttpClient httpClient,
    IOptions<FakePaymentProviderOptions> options,
    ILogger<FakePaymentProviderClient> logger) : IPaymentProviderClient
{
    private readonly FakePaymentProviderOptions _options = options.Value;

    public async Task SubmitPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
                                                                              var request = new FakeProviderPaymentRequest
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            MerchantOrderId = payment.MerchantOrderId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            CorrelationId = payment.CorrelationId,
            WebhookUrl = _options.WebhookUrl
        };

        using var response = await httpClient.PostAsJsonAsync(
            "/provider/payments",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var providerResponse = await response.Content.ReadFromJsonAsync<FakeProviderPaymentResponse>(
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Fake provider accepted payment {PaymentId} with provider payment {ProviderPaymentId}",
            payment.Id,
            providerResponse?.ProviderPaymentId);
    }
}
