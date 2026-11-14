using System.Data.Common;
using Careflow.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace Careflow.Api;

public sealed class CareflowExceptionHandler(IProblemDetailsService problems, ILogger<CareflowExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code, title) = exception switch
        {
            DomainException e => (e.StatusCode, e.Code, e.Message),
            BadHttpRequestException or System.Text.Json.JsonException => (400, "request.invalid", "The request contains missing or invalid fields."),
            DbException e when e.ErrorCode is -2146232060 or 2601 or 2627 => (409, "data.conflict", "The operation conflicts with an existing record or a concurrent change."),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "request.cancelled", "The request was cancelled."),
            _ => (500, "server.error", "The operation could not be completed. Contact support with the correlation ID.")
        };
        if (status >= 500) logger.LogError(exception, "Request failed. Trace={TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = new ProblemDetails { Status = status, Title = title, Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier } } });
    }
}
