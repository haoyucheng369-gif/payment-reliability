using PaymentFlowCloud.Worker;
using PaymentFlowCloud.Application;
using PaymentFlowCloud.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Worker 和 API 复用同一套 Application / Infrastructure 注册，避免运行时行为分叉。
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPaymentWorker();

var host = builder.Build();
host.Run();
