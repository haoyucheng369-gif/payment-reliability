using Microsoft.Extensions.DependencyInjection;
using ReliablePaymentProcessing.Application.Orders;
using ReliablePaymentProcessing.Application.Payments;

namespace ReliablePaymentProcessing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
                                                                  services.AddScoped<CreateOrderService>();
        services.AddScoped<GetOrderService>();
        services.AddScoped<CreatePaymentService>();
        services.AddScoped<GetPaymentService>();
        services.AddScoped<ProcessPaymentService>();
        services.AddScoped<CompletePaymentService>();

        return services;
    }
}
