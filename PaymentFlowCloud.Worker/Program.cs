using PaymentFlowCloud.Worker;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Application.Payments;
using PaymentFlowCloud.Infrastructure.Persistence;
using PaymentFlowCloud.Worker.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SqlServer"));
});

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMQ"));

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ProcessPaymentService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
