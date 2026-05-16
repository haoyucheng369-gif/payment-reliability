using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PaymentFlowCloud.Api.HealthChecks;

public static class HealthCheckResponseWriter
{
    public static async Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        // readiness 返回简洁 JSON，方便 Docker、Azure 和人工调试都能读取。
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    public static HealthCheckOptions CreateOptions()
    {
        return new HealthCheckOptions
        {
            ResponseWriter = WriteJsonAsync
        };
    }
}
