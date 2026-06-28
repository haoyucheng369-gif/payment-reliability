using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReliablePaymentProcessing.Application.Abstractions;
using ReliablePaymentProcessing.Infrastructure.Messaging;
using ReliablePaymentProcessing.Infrastructure.Persistence;
using ReliablePaymentProcessing.Infrastructure.Providers;

namespace ReliablePaymentProcessing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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
