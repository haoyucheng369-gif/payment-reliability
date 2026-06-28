namespace ReliablePaymentProcessing.Worker;

public class Worker(PaymentCreatedConsumer paymentCreatedConsumer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
                                                                              await paymentCreatedConsumer.StartAsync(stoppingToken);
    }
}
