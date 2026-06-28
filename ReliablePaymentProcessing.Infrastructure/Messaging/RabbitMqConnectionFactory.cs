using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ReliablePaymentProcessing.Infrastructure.Messaging;

public class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
{
    private readonly RabbitMqOptions _options = options.Value;

    public string QueueName => _options.QueueName;

    public string DeadLetterQueueName => _options.DeadLetterQueueName;

    public int MaxRetryCount => _options.MaxRetryCount;

                                                public ushort PrefetchCount => _options.PrefetchCount == 0
        ? (ushort)1
        : _options.PrefetchCount;

                                                    public int MaxConcurrentMessages => Math.Max(1, _options.MaxConcurrentMessages);

    public async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
                                                                   var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        return await factory.CreateConnectionAsync(cancellationToken);
    }

    public async Task DeclarePaymentCreatedQueuesAsync(
        IChannel channel,
        CancellationToken cancellationToken = default)
    {
                                                                           await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
