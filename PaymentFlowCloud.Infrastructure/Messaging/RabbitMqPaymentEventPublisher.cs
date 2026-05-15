using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.Domain.Entities;
using RabbitMQ.Client;

namespace PaymentFlowCloud.Infrastructure.Messaging;

public class RabbitMqPaymentEventPublisher : IPaymentEventPublisher
{
    private readonly RabbitMqOptions _options;

    public RabbitMqPaymentEventPublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task PublishPaymentCreatedAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        // 当前每次发布临时创建连接，足够支撑本地 MVP；后续可优化为长连接复用。
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // 发布前声明队列，保证本地环境首次启动时不依赖手工建队列。
        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var message = new PaymentCreatedMessage
        {
            PaymentId = payment.Id,
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
            routingKey: _options.QueueName,
            body: body,
            cancellationToken: cancellationToken);
    }
}
