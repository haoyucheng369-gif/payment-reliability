namespace PaymentFlowCloud.Worker;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentWorker(this IServiceCollection services)
    {
        // Worker 只注册消息消费入口，具体业务处理仍委托给 Application。
        services.AddSingleton<PaymentCreatedConsumer>();
        services.AddHostedService<Worker>();

        return services;
    }
}
