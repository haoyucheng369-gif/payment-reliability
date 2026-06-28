using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ReliablePaymentProcessing.Api.Errors;
using ReliablePaymentProcessing.Api.HealthChecks;
using ReliablePaymentProcessing.Api.Observability;
using ReliablePaymentProcessing.Api.Security;
using ReliablePaymentProcessing.Application;
using ReliablePaymentProcessing.Application.Observability;
using ReliablePaymentProcessing.Infrastructure;
using ReliablePaymentProcessing.Infrastructure.Persistence;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341");
});

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.Configure<FakeProviderWebhookOptions>(
    builder.Configuration.GetSection("FakeProviderWebhook"));
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource.AddService("ReliablePaymentProcessing.Api");
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(ReliablePaymentProcessingTelemetry.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(
                    builder.Configuration["OpenTelemetry:OtlpEndpoint"]
                    ?? "http://localhost:4317");
            });

        var applicationInsightsConnectionString =
            builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? builder.Configuration["ApplicationInsights:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
        {
                                                                                                    tracing.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = applicationInsightsConnectionString;
            });
        }
    });
builder.Services
    .AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sqlserver")
    .AddCheck<RabbitMqHealthCheck>("rabbitmq");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await ApplyDatabaseMigrationsAsync(app);
}

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();

app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapHealthChecks("/health/ready", HealthCheckResponseWriter.CreateOptions());

app.MapMetrics();

app.MapControllers();

app.Run();

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            return;
        }
        catch when (attempt < maxAttempts)
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}
