using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ReliablePaymentProcessing.Application.Abstractions;
using ReliablePaymentProcessing.Application.Contracts;
using ReliablePaymentProcessing.Application.Observability;
using ReliablePaymentProcessing.Application.Payments;
using ReliablePaymentProcessing.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ReliablePaymentProcessing.Worker;

public class PaymentCreatedConsumer(
    ILogger<PaymentCreatedConsumer> logger,
    IServiceScopeFactory serviceScopeFactory,
    RabbitMqConnectionFactory connectionFactory)
{
    private const string RetryCountHeader = "x-retry-count";

    public async Task StartAsync(CancellationToken stoppingToken)
    {
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
                                                                                 var delivery = MessageDelivery.From(eventArgs);

                                                                                              await messageProcessingSemaphore.WaitAsync(stoppingToken);

            _ = Task.Run(async () =>
            {
                try
                {
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

                                                                          using var serviceScope = serviceScopeFactory.CreateScope();
            var getPaymentService = serviceScope.ServiceProvider.GetRequiredService<GetPaymentService>();
            var paymentProviderClient = serviceScope.ServiceProvider.GetRequiredService<IPaymentProviderClient>();
            var processPaymentService = serviceScope.ServiceProvider.GetRequiredService<ProcessPaymentService>();

            var payment = await getPaymentService.GetByIdAsync(message.PaymentId, cancellationToken);
            if (payment is null)
            {
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
                                                                       await RetryOrMoveToDeadLetterQueueAsync(
                    channel,
                    delivery,
                    channelOperationSemaphore,
                    cancellationToken);
                return;
            }

                                                               await AckAsync(channel, delivery, channelOperationSemaphore, cancellationToken);
            logger.LogInformation(
                "Payment-created message submitted to provider for payment {PaymentId}",
                message.PaymentId);
        }
        catch (Exception ex)
        {
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
            ? ReliablePaymentProcessingTelemetry.ActivitySource.StartActivity(
                "rabbitmq consume payment-created",
                ActivityKind.Consumer)
            : ReliablePaymentProcessingTelemetry.ActivitySource.StartActivity(
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
        var traceParent = GetHeaderAsString(delivery, ReliablePaymentProcessingTelemetry.TraceParentHeaderName);
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return null;
        }

        var traceState = GetHeaderAsString(delivery, ReliablePaymentProcessingTelemetry.TraceStateHeaderName);
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
