using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Infrastructure.Messaging;
using PaymentFlowCloud.Infrastructure.Persistence;
using PaymentFlowCloud.Infrastructure.Providers;

namespace PaymentFlowCloud.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 基础设施层集中绑定数据库、消息队列和外部支付提供方实现。
        services.AddDbContext<PaymentDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("SqlServer"));
        });

        services.Configure<RabbitMqOptions>(
            configuration.GetSection("RabbitMQ"));
        services.Configure<FakePaymentProviderOptions>(
            configuration.GetSection("FakeProvider"));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddSingleton<IPaymentEventPublisher, RabbitMqPaymentEventPublisher>();

        services.AddHttpClient<IPaymentProviderClient, FakePaymentProviderClient>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<FakePaymentProviderOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });

        return services;
    }
}
