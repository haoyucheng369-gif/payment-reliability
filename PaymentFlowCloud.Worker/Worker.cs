using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentFlowCloud.Application.Contracts;
using PaymentFlowCloud.Application.Payments;
using PaymentFlowCloud.Worker.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentFlowCloud.Worker;

public class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<RabbitMqOptions> options) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

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
            using var scope = serviceScopeFactory.CreateScope();
            var processPaymentService = scope.ServiceProvider.GetRequiredService<ProcessPaymentService>();

            var processed = await processPaymentService.MarkProcessedAsync(
                message.PaymentId,
                cancellationToken);
            if (!processed)
            {
                logger.LogWarning("Payment {PaymentId} was not found; requeueing message", message.PaymentId);
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
                return;
            }

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
            logger.LogInformation(
                "Processed payment {PaymentId} with trace {TraceId}",
                message.PaymentId,
                message.TraceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process payment-created message");
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }
}
