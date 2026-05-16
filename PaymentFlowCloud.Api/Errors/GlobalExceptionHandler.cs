using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PaymentFlowCloud.Application.Common;

namespace PaymentFlowCloud.Api.Errors;

public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad request"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled API exception");
        }
        else
        {
            logger.LogWarning(exception, "Handled API exception with status {StatusCode}", statusCode);
        }

        httpContext.Response.StatusCode = statusCode;

        // 统一使用 RFC 7807 ProblemDetails，避免 controller 到处手写错误响应。
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message,
                Instance = httpContext.Request.Path
            }
        });
    }
}
