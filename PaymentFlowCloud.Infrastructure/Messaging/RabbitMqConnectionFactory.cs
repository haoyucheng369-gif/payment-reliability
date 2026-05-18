using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace PaymentFlowCloud.Infrastructure.Messaging;

public class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
{
    private readonly RabbitMqOptions _options = options.Value;

    public string QueueName => _options.QueueName;

    public string DeadLetterQueueName => _options.DeadLetterQueueName;

    public int MaxRetryCount => _options.MaxRetryCount;

    // 防止配置成 0 后 consumer 没有限制地预取消息。
    public ushort PrefetchCount => _options.PrefetchCount == 0
        ? (ushort)1
        : _options.PrefetchCount;

    // 防止配置成 0 或负数后 Worker 永远无法取得并发槽位。
    public int MaxConcurrentMessages => Math.Max(1, _options.MaxConcurrentMessages);

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

    public async Task DeclarePaymentCreatedQueuesAsync(
        IChannel channel,
        CancellationToken cancellationToken = default)
    {
        // 本地环境启动时自动声明主队列和 DLQ，避免依赖手工创建 RabbitMQ queue。
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
