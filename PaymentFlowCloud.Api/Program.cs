using Microsoft.EntityFrameworkCore;
using PaymentFlowCloud.Api.Contracts;
using PaymentFlowCloud.Api.Messaging;
using PaymentFlowCloud.Domain.Entities;
using PaymentFlowCloud.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<PaymentDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SqlServer"));
});

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IPaymentEventPublisher, RabbitMqPaymentEventPublisher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/payments", async (
    CreatePaymentRequest request,
    PaymentDbContext dbContext,
    IPaymentEventPublisher paymentEventPublisher,
    CancellationToken cancellationToken) =>
{
    var payment = new Payment
    {
        Id = Guid.NewGuid(),
        MerchantOrderId = request.MerchantOrderId,
        Amount = request.Amount,
        Currency = request.Currency,
        Status = "Pending",
        TraceId = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow
    };

    dbContext.Payments.Add(payment);

    await dbContext.SaveChangesAsync(cancellationToken);

    await paymentEventPublisher.PublishPaymentCreatedAsync(payment, cancellationToken);

    return Results.Ok(payment);
});

app.Run();
