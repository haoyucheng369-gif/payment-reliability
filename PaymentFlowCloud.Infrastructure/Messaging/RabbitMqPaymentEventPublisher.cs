using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.Domain.Entities;
using RabbitMQ.Client;

namespace PaymentFlowCloud.Infrastructure.Messaging;

public class RabbitMqPaymentEventPublisher(
    RabbitMqConnectionFactory connectionFactory,
    ILogger<RabbitMqPaymentEventPublisher> logger) : IPaymentEventPublisher
{
    public async Task PublishPaymentCreatedAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        // 当前每次发布临时创建连接，足够支撑本地 MVP；后续可优化为长连接复用。
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await connectionFactory.DeclarePaymentCreatedQueueAsync(channel, cancellationToken);

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

        // 使用默认 exchange，routingKey 直接指向队列名。
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: connectionFactory.QueueName,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Published payment-created message for payment {PaymentId} to queue {QueueName}",
            payment.Id,
            connectionFactory.QueueName);
    }
}
