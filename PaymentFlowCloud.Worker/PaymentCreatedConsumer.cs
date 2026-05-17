using System.Text.Json;
using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.Application.Payments;
using PaymentFlowCloud.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentFlowCloud.Worker;

public class PaymentCreatedConsumer(
    ILogger<PaymentCreatedConsumer> logger,
    IServiceScopeFactory serviceScopeFactory,
    RabbitMqConnectionFactory connectionFactory)
{
    private const string RetryCountHeader = "x-retry-count";

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        // 当前 consumer 持有一个连接和 channel，生命周期跟随 Worker 进程。
        await using var connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await connectionFactory.DeclarePaymentCreatedQueuesAsync(channel, stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            // 每条消息独立处理，成功 ack，失败按 retry count 重新发布或进入 DLQ。
            await HandleMessageAsync(channel, eventArgs, stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: connectionFactory.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Payment worker consuming queue {QueueName} with DLQ {DeadLetterQueueName}",
            connectionFactory.QueueName,
            connectionFactory.DeadLetterQueueName);

        try
        {
            // 保持后台服务存活，直到主机发出停止信号。
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        PaymentCreatedMessage? message;

        try
        {
            // 消息格式错误不可恢复，直接送入 DLQ，避免无限重试。
            message = JsonSerializer.Deserialize<PaymentCreatedMessage>(eventArgs.Body.Span);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Moving invalid payment-created message to DLQ");
            await MoveToDeadLetterQueueAsync(channel, eventArgs, cancellationToken);
            return;
        }

        if (message is null || message.PaymentId == Guid.Empty)
        {
            logger.LogWarning("Moving empty payment-created message to DLQ");
            await MoveToDeadLetterQueueAsync(channel, eventArgs, cancellationToken);
            return;
        }

        // Worker 没有 HTTP pipeline，所以用消息体里的 CorrelationId 手动创建日志 scope。
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["PaymentId"] = message.PaymentId
        });

        try
        {
            logger.LogInformation(
                "Consuming payment-created message for payment {PaymentId} and order {OrderId}",
                message.PaymentId,
                message.OrderId);

            // 每条消息创建独立 DI scope，确保 DbContext 生命周期正确。
            using var serviceScope = serviceScopeFactory.CreateScope();
            var processPaymentService = serviceScope.ServiceProvider.GetRequiredService<ProcessPaymentService>();

            var processed = await processPaymentService.MarkProcessedAsync(
                message.PaymentId,
                cancellationToken);
            if (!processed)
            {
                // 支付记录暂时查不到时按固定次数重试，超过后进入 DLQ。
                await RetryOrMoveToDeadLetterQueueAsync(channel, eventArgs, cancellationToken);
                return;
            }

            // 应用层处理成功后再 ack，确保消息不会提前丢失。
            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
            logger.LogInformation(
                "Processed payment-created message for payment {PaymentId}",
                message.PaymentId);
        }
        catch (Exception ex)
        {
            // 处理失败时不再无限 requeue，而是按 retry count 重新发布或进入 DLQ。
            logger.LogError(ex, "Failed to process payment-created message");
            await RetryOrMoveToDeadLetterQueueAsync(channel, eventArgs, cancellationToken);
        }
    }

    private async Task RetryOrMoveToDeadLetterQueueAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        var retryCount = GetRetryCount(eventArgs);

        if (retryCount >= connectionFactory.MaxRetryCount)
        {
            logger.LogWarning(
                "Moving payment-created message to DLQ after {RetryCount} retries",
                retryCount);

            await MoveToDeadLetterQueueAsync(channel, eventArgs, cancellationToken);
            return;
        }

        var nextRetryCount = retryCount + 1;

        logger.LogWarning(
            "Retrying payment-created message, retry {RetryCount} of {MaxRetryCount}",
            nextRetryCount,
            connectionFactory.MaxRetryCount);

        await PublishCopyAsync(
            channel,
            eventArgs,
            connectionFactory.QueueName,
            nextRetryCount,
            cancellationToken);

        await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
    }

    private async Task MoveToDeadLetterQueueAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        await PublishCopyAsync(
            channel,
            eventArgs,
            connectionFactory.DeadLetterQueueName,
            GetRetryCount(eventArgs),
            cancellationToken);

        await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
    }

    private static async Task PublishCopyAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        string routingKey,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var headers = CopyHeaders(eventArgs);
        headers[RetryCountHeader] = retryCount;

        var properties = new BasicProperties
        {
            ContentType = eventArgs.BasicProperties.ContentType,
            CorrelationId = eventArgs.BasicProperties.CorrelationId,
            MessageId = eventArgs.BasicProperties.MessageId,
            Persistent = true,
            Headers = headers
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: eventArgs.Body,
            cancellationToken: cancellationToken);
    }

    private static Dictionary<string, object?> CopyHeaders(BasicDeliverEventArgs eventArgs)
    {
        return eventArgs.BasicProperties.Headers is null
            ? new Dictionary<string, object?>()
            : eventArgs.BasicProperties.Headers.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
    }

    private static int GetRetryCount(BasicDeliverEventArgs eventArgs)
    {
        if (eventArgs.BasicProperties.Headers is null
            || !eventArgs.BasicProperties.Headers.TryGetValue(RetryCountHeader, out var value))
        {
            return 0;
        }

        return value switch
        {
            byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            int retryCount => retryCount,
            long retryCount => (int)retryCount,
            short retryCount => retryCount,
            byte retryCount => retryCount,
            _ => 0
        };
    }
}
