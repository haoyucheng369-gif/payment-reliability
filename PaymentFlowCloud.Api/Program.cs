using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PaymentFlowCloud.Application;
using PaymentFlowCloud.Infrastructure;
using PaymentFlowCloud.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 注册 API 文档和 JSON 序列化规则，枚举统一以字符串形式输出。
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 注册应用层用例和基础设施实现，Program.cs 只保留组合根职责。
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await ApplyDatabaseMigrationsAsync(app);
}

// 开发环境暴露 OpenAPI 描述和 Swagger UI，便于本地直接调试接口。
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 使用标准 Controller 路由，支付 API 入口集中在 PaymentsController。
app.MapControllers();

app.Run();

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

    // Docker Compose 首次启动时 SQL Server 可能刚变为 healthy 但还没完全可用，这里做短重试保证一键启动稳定。
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
