using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace PaymentFlowCloud.Infrastructure.Messaging;

public class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
{
    private readonly RabbitMqOptions _options = options.Value;

    public string QueueName => _options.QueueName;

    public async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        // 集中创建 RabbitMQ 连接，避免发布端和消费端重复维护连接参数。
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        return await factory.CreateConnectionAsync(cancellationToken);
    }

    public async Task DeclarePaymentCreatedQueueAsync(
        IChannel channel,
        CancellationToken cancellationToken = default)
    {
        // 本地环境启动时自动声明队列，避免依赖手工创建 RabbitMQ queue。
        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
