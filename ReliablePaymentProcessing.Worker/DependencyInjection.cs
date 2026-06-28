namespace ReliablePaymentProcessing.Worker;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentWorker(this IServiceCollection services)
    {
                                                                      services.AddSingleton<PaymentCreatedConsumer>();
        services.AddHostedService<Worker>();

        return services;
    }
}
