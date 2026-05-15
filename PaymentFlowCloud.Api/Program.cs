using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PaymentFlowCloud.Application.Abstractions;
using PaymentFlowCloud.Application.Payments;
using PaymentFlowCloud.Api.Contracts;
using PaymentFlowCloud.Infrastructure.Messaging;
using PaymentFlowCloud.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDbContext<PaymentDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SqlServer"));
});

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IPaymentEventPublisher, RabbitMqPaymentEventPublisher>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<CreatePaymentService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/payments", async (
    CreatePaymentRequest request,
    CreatePaymentService createPaymentService,
    CancellationToken cancellationToken) =>
{
    var payment = await createPaymentService.CreateAsync(
        new CreatePaymentCommand
        {
            MerchantOrderId = request.MerchantOrderId,
            Amount = request.Amount,
            Currency = request.Currency
        },
        cancellationToken);

    return Results.Ok(payment);
});

app.Run();
