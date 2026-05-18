namespace PaymentFlowCloud.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string QueueName { get; set; } = "payment-created";

    public string DeadLetterQueueName { get; set; } = "payment-created-dlq";

    public int MaxRetryCount { get; set; } = 3;

    // RabbitMQ 一次最多推送给当前 consumer 的未 ack 消息数。
    public ushort PrefetchCount { get; set; } = 10;

    // Worker 本进程内同时处理 payment-created 消息的最大数量。
    public int MaxConcurrentMessages { get; set; } = 5;
}
