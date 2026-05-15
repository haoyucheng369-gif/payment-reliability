namespace PaymentFlowCloud.Worker;

public class Worker(PaymentCreatedConsumer paymentCreatedConsumer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // BackgroundService 只负责托管生命周期，消费细节放在独立 consumer 中。
        await paymentCreatedConsumer.StartAsync(stoppingToken);
    }
}
