using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentFlowCloud.Api.Contracts;
using PaymentFlowCloud.Domain.Entities;
using RabbitMQ.Client;

namespace PaymentFlowCloud.Api.Messaging;

public class RabbitMqPaymentEventPublisher : IPaymentEventPublisher
{
    private readonly RabbitMqOptions _options;

    public RabbitMqPaymentEventPublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task PublishPaymentCreatedAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

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
            TraceId = payment.TraceId,
            CreatedAt = payment.CreatedAt
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _options.QueueName,
            body: body,
            cancellationToken: cancellationToken);
    }
}
