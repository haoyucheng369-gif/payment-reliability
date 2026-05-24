using PaymentFlowCloud.Application;
using PaymentFlowCloud.Application.Observability;
using PaymentFlowCloud.Infrastructure;
using PaymentFlowCloud.Worker;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) =>
{
    // Worker 日志和 API 一样写入 Console + Seq，框架日志降噪后方便按 CorrelationId 查询异步链路。
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341");
});

// Worker 和 API 复用同一套 Application / Infrastructure 注册，避免运行时行为分叉。
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource.AddService("PaymentFlowCloud.Worker");
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(PaymentFlowCloudTelemetry.ActivitySourceName)
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
            // 配置了 Application Insights 连接字符串时，同一份 trace 会额外发送到 Azure Monitor。
            tracing.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = applicationInsightsConnectionString;
            });
        }
    });
builder.Services.AddPaymentWorker();

var host = builder.Build();
host.Run();
