using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.Application.Observability;
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
        using var messageProcessingSemaphore = new SemaphoreSlim(connectionFactory.MaxConcurrentMessages);
        using var channelOperationSemaphore = new SemaphoreSlim(1, 1);

        await connectionFactory.DeclarePaymentCreatedQueuesAsync(channel, stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: connectionFactory.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            // RabbitMQ.Client 的 Body 只在回调期间可靠；并发处理前先复制快照。
            var delivery = MessageDelivery.From(eventArgs);

            // 先取得并发槽位，再把消息处理放到后台任务，允许 RabbitMQ 继续投递到 prefetch 上限。
            await messageProcessingSemaphore.WaitAsync(stoppingToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    // 每条消息独立处理；成功 ack，失败按 retry count 重新发布或进入 DLQ。
                    await HandleMessageAsync(
                        channel,
                        delivery,
                        channelOperationSemaphore,
                        stoppingToken);
                }
                finally
                {
                    messageProcessingSemaphore.Release();
                }
            }, CancellationToken.None);
        };

        await channel.BasicConsumeAsync(
            queue: connectionFactory.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Payment worker consuming queue {QueueName} with DLQ {DeadLetterQueueName}, prefetch {PrefetchCount}, max concurrency {MaxConcurrentMessages}",
            connectionFactory.QueueName,
            connectionFactory.DeadLetterQueueName,
            connectionFactory.PrefetchCount,
            connectionFactory.MaxConcurrentMessages);

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
        MessageDelivery delivery,
        SemaphoreSlim channelOperationSemaphore,
        CancellationToken cancellationToken)
    {
        PaymentCreatedMessage? message;

        try
        {
            // 消息格式错误不可恢复，直接送入 DLQ，避免无限重试。
            message = JsonSerializer.Deserialize<PaymentCreatedMessage>(delivery.Body.Span);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Moving invalid payment-created message to DLQ");
            await MoveToDeadLetterQueueAsync(
                channel,
                delivery,
                channelOperationSemaphore,
                cancellationToken);
            return;
        }

        if (message is null || message.PaymentId == Guid.Empty)
        {
            logger.LogWarning("Moving empty payment-created message to DLQ");
            await MoveToDeadLetterQueueAsync(
                channel,
                delivery,
                channelOperationSemaphore,
                cancellationToken);
            return;
        }

        using var activity = StartConsumerActivity(delivery, message);

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
            var getPaymentService = serviceScope.ServiceProvider.GetRequiredService<GetPaymentService>();
            var paymentProviderClient = serviceScope.ServiceProvider.GetRequiredService<IPaymentProviderClient>();
            var processPaymentService = serviceScope.ServiceProvider.GetRequiredService<ProcessPaymentService>();

            var payment = await getPaymentService.GetByIdAsync(message.PaymentId, cancellationToken);
            if (payment is null)
            {
                // 支付记录暂时查不到时按固定次数重试，超过后进入 DLQ。
                await RetryOrMoveToDeadLetterQueueAsync(
                    channel,
                    delivery,
                    channelOperationSemaphore,
                    cancellationToken);
                return;
            }

            await paymentProviderClient.SubmitPaymentAsync(payment, cancellationToken);

            var processingStarted = await processPaymentService.MarkProcessingAsync(
                message.PaymentId,
                cancellationToken);
            if (!processingStarted)
            {
                // 状态推进失败时按固定次数重试，超过后进入 DLQ。
                await RetryOrMoveToDeadLetterQueueAsync(
                    channel,
                    delivery,
                    channelOperationSemaphore,
                    cancellationToken);
                return;
            }

            // 应用层处理成功后再 ack，确保消息不会提前丢失。
            await AckAsync(channel, delivery, channelOperationSemaphore, cancellationToken);
            logger.LogInformation(
                "Payment-created message submitted to provider for payment {PaymentId}",
                message.PaymentId);
        }
        catch (Exception ex)
        {
            // 处理失败时不无限 requeue，而是按 retry count 重新发布或进入 DLQ。
            logger.LogError(ex, "Failed to process payment-created message");
            await RetryOrMoveToDeadLetterQueueAsync(
                channel,
                delivery,
                channelOperationSemaphore,
                cancellationToken);
        }
    }

    private async Task RetryOrMoveToDeadLetterQueueAsync(
        IChannel channel,
        MessageDelivery delivery,
        SemaphoreSlim channelOperationSemaphore,
        CancellationToken cancellationToken)
    {
        var retryCount = GetRetryCount(delivery);

        if (retryCount >= connectionFactory.MaxRetryCount)
        {
            logger.LogWarning(
                "Moving payment-created message to DLQ after {RetryCount} retries",
                retryCount);

            await MoveToDeadLetterQueueAsync(
                channel,
                delivery,
                channelOperationSemaphore,
                cancellationToken);
            return;
        }

        var nextRetryCount = retryCount + 1;

        logger.LogWarning(
            "Retrying payment-created message, retry {RetryCount} of {MaxRetryCount}",
            nextRetryCount,
            connectionFactory.MaxRetryCount);

        await channelOperationSemaphore.WaitAsync(cancellationToken);
        try
        {
            await PublishCopyAsync(
                channel,
                delivery,
                connectionFactory.QueueName,
                nextRetryCount,
                cancellationToken);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        finally
        {
            channelOperationSemaphore.Release();
        }
    }

    private async Task MoveToDeadLetterQueueAsync(
        IChannel channel,
        MessageDelivery delivery,
        SemaphoreSlim channelOperationSemaphore,
        CancellationToken cancellationToken)
    {
        await channelOperationSemaphore.WaitAsync(cancellationToken);
        try
        {
            await PublishCopyAsync(
                channel,
                delivery,
                connectionFactory.DeadLetterQueueName,
                GetRetryCount(delivery),
                cancellationToken);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        finally
        {
            channelOperationSemaphore.Release();
        }
    }

    private static async Task AckAsync(
        IChannel channel,
        MessageDelivery delivery,
        SemaphoreSlim channelOperationSemaphore,
        CancellationToken cancellationToken)
    {
        await channelOperationSemaphore.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        finally
        {
            channelOperationSemaphore.Release();
        }
    }

    private static async Task PublishCopyAsync(
        IChannel channel,
        MessageDelivery delivery,
        string routingKey,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var headers = CopyHeaders(delivery);
        headers[RetryCountHeader] = retryCount;

        var properties = new BasicProperties
        {
            ContentType = delivery.ContentType,
            CorrelationId = delivery.CorrelationId,
            MessageId = delivery.MessageId,
            Persistent = true,
            Headers = headers
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: delivery.Body,
            cancellationToken: cancellationToken);
    }

    private Activity? StartConsumerActivity(
        MessageDelivery delivery,
        PaymentCreatedMessage message)
    {
        var parentContext = ExtractParentContext(delivery);
        var activity = parentContext is null
            ? PaymentFlowCloudTelemetry.ActivitySource.StartActivity(
                "rabbitmq consume payment-created",
                ActivityKind.Consumer)
            : PaymentFlowCloudTelemetry.ActivitySource.StartActivity(
                "rabbitmq consume payment-created",
                ActivityKind.Consumer,
                parentContext.Value);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", connectionFactory.QueueName);
        activity?.SetTag("messaging.operation.name", "process");
        activity?.SetTag("payment.id", message.PaymentId);
        activity?.SetTag("order.id", message.OrderId);
        activity?.SetTag("correlation.id", message.CorrelationId);

        return activity;
    }

    private static ActivityContext? ExtractParentContext(MessageDelivery delivery)
    {
        var traceParent = GetHeaderAsString(delivery, PaymentFlowCloudTelemetry.TraceParentHeaderName);
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return null;
        }

        var traceState = GetHeaderAsString(delivery, PaymentFlowCloudTelemetry.TraceStateHeaderName);
        return ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var context)
            ? context
            : null;
    }

    private static string? GetHeaderAsString(MessageDelivery delivery, string headerName)
    {
        if (delivery.Headers is null
            || !delivery.Headers.TryGetValue(headerName, out var value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value?.ToString()
        };
    }

    private static Dictionary<string, object?> CopyHeaders(MessageDelivery delivery)
    {
        return delivery.Headers is null
            ? new Dictionary<string, object?>()
            : delivery.Headers.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
    }

    private static int GetRetryCount(MessageDelivery delivery)
    {
        if (delivery.Headers is null
            || !delivery.Headers.TryGetValue(RetryCountHeader, out var value))
        {
            return 0;
        }

        return value switch
        {
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            int retryCount => retryCount,
            long retryCount => (int)retryCount,
            short retryCount => retryCount,
            byte retryCount => retryCount,
            _ => 0
        };
    }

    private sealed record MessageDelivery(
        ulong DeliveryTag,
        ReadOnlyMemory<byte> Body,
        string? ContentType,
        string? CorrelationId,
        string? MessageId,
        Dictionary<string, object?>? Headers)
    {
        public static MessageDelivery From(BasicDeliverEventArgs eventArgs)
        {
            return new MessageDelivery(
                eventArgs.DeliveryTag,
                eventArgs.Body.ToArray(),
                eventArgs.BasicProperties.ContentType,
                eventArgs.BasicProperties.CorrelationId,
                eventArgs.BasicProperties.MessageId,
                eventArgs.BasicProperties.Headers?.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value));
        }
    }
}
