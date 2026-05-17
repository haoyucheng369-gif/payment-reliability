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
}
