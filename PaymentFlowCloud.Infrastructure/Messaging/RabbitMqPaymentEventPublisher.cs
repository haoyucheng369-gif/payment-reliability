using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.Application.Observability;
using PaymentFlowCloud.Domain.Entities;
using RabbitMQ.Client;

namespace PaymentFlowCloud.Infrastructure.Messaging;

public class RabbitMqPaymentEventPublisher(
    RabbitMqConnectionFactory connectionFactory,
    ILogger<RabbitMqPaymentEventPublisher> logger) : IPaymentEventPublisher
{
    public async Task PublishPaymentCreatedAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        using var activity = PaymentFlowCloudTelemetry.ActivitySource.StartActivity(
            "rabbitmq publish payment-created",
            ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", connectionFactory.QueueName);
        activity?.SetTag("messaging.operation.name", "publish");
        activity?.SetTag("payment.id", payment.Id);
        activity?.SetTag("order.id", payment.OrderId);
        activity?.SetTag("correlation.id", payment.CorrelationId);

        // 当前每次发布临时创建连接，足够支撑本地版本；后续可优化为长连接复用。
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await connectionFactory.DeclarePaymentCreatedQueuesAsync(channel, cancellationToken);

        var message = new PaymentCreatedMessage
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            MerchantOrderId = payment.MerchantOrderId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            CorrelationId = payment.CorrelationId,
            CreatedAt = payment.CreatedAt
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var headers = new Dictionary<string, object?>();
        AddTraceContextHeaders(headers);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            CorrelationId = payment.CorrelationId,
            MessageId = payment.Id.ToString(),
            Persistent = true,
            Headers = headers
        };

        // 使用默认 exchange，routing key 直接指向队列名。
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: connectionFactory.QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Published payment-created message for payment {PaymentId} to queue {QueueName}",
            payment.Id,
            connectionFactory.QueueName);
    }

    private static void AddTraceContextHeaders(Dictionary<string, object?> headers)
    {
        var currentActivity = Activity.Current;
        if (currentActivity?.Id is not null)
        {
            headers[PaymentFlowCloudTelemetry.TraceParentHeaderName] = Encoding.UTF8.GetBytes(currentActivity.Id);
        }

        if (!string.IsNullOrWhiteSpace(currentActivity?.TraceStateString))
        {
            headers[PaymentFlowCloudTelemetry.TraceStateHeaderName] =
                Encoding.UTF8.GetBytes(currentActivity.TraceStateString);
        }
    }
}
