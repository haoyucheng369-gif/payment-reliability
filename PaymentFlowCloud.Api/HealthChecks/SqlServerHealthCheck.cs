using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PaymentFlowCloud.Infrastructure.Persistence;

namespace PaymentFlowCloud.Api.HealthChecks;

public class SqlServerHealthCheck(PaymentDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // readiness 检查真实数据库连接，避免 API 已启动但 SQL Server 不可用。
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("SQL Server is reachable.")
            : HealthCheckResult.Unhealthy("SQL Server is not reachable.");
    }
}
