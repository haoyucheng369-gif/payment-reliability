using Microsoft.Extensions.Diagnostics.HealthChecks;
using PaymentFlowCloud.Infrastructure.Messaging;

namespace PaymentFlowCloud.Api.HealthChecks;

public class RabbitMqHealthCheck(RabbitMqConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // readiness 检查真实 RabbitMQ 连接，确保 API 能发布支付事件。
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
