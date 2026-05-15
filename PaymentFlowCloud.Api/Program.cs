using System.Text.Json.Serialization;
using PaymentFlowCloud.Application;
using PaymentFlowCloud.Infrastructure;

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
