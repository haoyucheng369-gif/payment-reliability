using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.Application.Payments;
using PaymentFlowCloud.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentFlowCloud.Worker;

public class PaymentCreatedConsumer(
    ILogger<PaymentCreatedConsumer> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<RabbitMqOptions> options)
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        // 当前 consumer 持有一个连接和 channel，生命周期跟随 Worker 进程。
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Worker 启动时声明队列，避免依赖 API 先启动。
        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            // 每条消息独立处理，成功 ack，失败按类型决定丢弃或重回队列。
            await HandleMessageAsync(channel, eventArgs, stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("Payment worker consuming queue {QueueName}", _options.QueueName);

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
            // 消息格式错误不可恢复，直接丢弃，避免无限重试。
            message = JsonSerializer.Deserialize<PaymentCreatedMessage>(eventArgs.Body.Span);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Discarding invalid payment-created message");
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            return;
        }

        if (message is null || message.PaymentId == Guid.Empty)
        {
            logger.LogWarning("Discarding empty payment-created message");
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            return;
        }

        try
        {
            // 每条消息创建独立 DI scope，确保 DbContext 生命周期正确。
            using var scope = serviceScopeFactory.CreateScope();
            var processPaymentService = scope.ServiceProvider.GetRequiredService<ProcessPaymentService>();

            var processed = await processPaymentService.MarkProcessedAsync(
                message.PaymentId,
                cancellationToken);
            if (!processed)
            {
                // 支付记录暂时查不到时保守重回队列，后续会替换成重试/DLQ 策略。
                logger.LogWarning("Payment {PaymentId} was not found; requeueing message", message.PaymentId);
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
                return;
            }

            // 应用层处理成功后再 ack，确保消息不会提前丢失。
            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
            logger.LogInformation(
                "Processed payment {PaymentId} with correlation {CorrelationId}",
                message.PaymentId,
                message.CorrelationId);
        }
        catch (Exception ex)
        {
            // 当前阶段先重回队列，后续接入最大重试次数和死信队列。
            logger.LogError(ex, "Failed to process payment-created message");
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }
}
