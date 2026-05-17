using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PaymentFlowCloud.Api.Errors;
using PaymentFlowCloud.Api.HealthChecks;
using PaymentFlowCloud.Api.Observability;
using PaymentFlowCloud.Application;
using PaymentFlowCloud.Infrastructure;
using PaymentFlowCloud.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    // 本地写入 Console 和 Seq，框架日志降噪后优先展示业务链路日志。
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341");
});

// 注册 API 文档、统一错误响应和 JSON 序列化规则，枚举统一以字符串形式输出。
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 注册应用层用例和基础设施实现，Program.cs 只保留组合根职责。
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sqlserver")
    .AddCheck<RabbitMqHealthCheck>("rabbitmq");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await ApplyDatabaseMigrationsAsync(app);
}

// CorrelationId 必须最早进入 pipeline，后续异常处理、controller、service 日志才能自动带上。
app.UseMiddleware<CorrelationIdMiddleware>();

// 全局异常处理统一输出 ProblemDetails，避免各 controller 分散处理通用异常。
app.UseExceptionHandler();

// 开发环境暴露 OpenAPI 描述和 Swagger UI，便于本地直接调试接口。
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// liveness 只表示 API 进程存活，不依赖 SQL Server 或 RabbitMQ。
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

// readiness 检查下游依赖，供 Docker/Azure 判断服务是否真正可接流量。
app.MapHealthChecks("/health/ready", HealthCheckResponseWriter.CreateOptions());

// 使用标准 Controller 路由，支付和订单入口集中在 Controllers 目录。
app.MapControllers();

app.Run();

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

    // Docker Compose 首次启动时 SQL Server 可能刚变为 healthy 但尚未完全可用，这里短重试提升一键启动稳定性。
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
