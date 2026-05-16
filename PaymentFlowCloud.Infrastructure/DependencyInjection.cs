using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Infrastructure.Messaging;
using PaymentFlowCloud.Infrastructure.Persistence;

namespace PaymentFlowCloud.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 基础设施层集中绑定数据库和消息队列实现，API/Worker 只调用扩展方法。
        services.AddDbContext<PaymentDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("SqlServer"));
        });

        services.Configure<RabbitMqOptions>(
            configuration.GetSection("RabbitMQ"));

        // 应用层依赖抽象，具体实现统一在 Infrastructure 里注册。
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddSingleton<IPaymentEventPublisher, RabbitMqPaymentEventPublisher>();

        return services;
    }
}
