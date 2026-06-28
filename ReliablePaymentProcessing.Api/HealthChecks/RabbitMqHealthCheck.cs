using Microsoft.Extensions.Diagnostics.HealthChecks;
using ReliablePaymentProcessing.Infrastructure.Messaging;

namespace ReliablePaymentProcessing.Api.HealthChecks;

public class RabbitMqHealthCheck(RabbitMqConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
                                                                              await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await connectionFactory.DeclarePaymentCreatedQueuesAsync(channel, cancellationToken);

            return HealthCheckResult.Healthy("RabbitMQ is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is not reachable.", ex);
        }
    }
}
