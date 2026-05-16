using Microsoft.Extensions.DependencyInjection;
using PaymentFlowCloud.Application.Orders;
using PaymentFlowCloud.Application.Payments;

namespace PaymentFlowCloud.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // 应用层只注册业务用例，不绑定 EF、RabbitMQ 等基础设施实现。
        services.AddScoped<CreateOrderService>();
        services.AddScoped<GetOrderService>();
        services.AddScoped<CreatePaymentService>();
        services.AddScoped<GetPaymentService>();
        services.AddScoped<ProcessPaymentService>();

        return services;
    }
}
