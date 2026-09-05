using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Api.Infrastructure;

/// <summary>Turns unhandled exceptions into consistent ProblemDetails without leaking internals to clients.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested) return true; // client went away; nothing to report
        var (status, code, detail) = exception switch
        {
            DbUpdateConcurrencyException => (409, "concurrency_conflict", "The record was changed by someone else. Reload it and try again."),
            DbUpdateException => (409, "data_conflict", "The change conflicts with existing data, for example a duplicate value."),
            BadHttpRequestException bad => (bad.StatusCode, "invalid_request", "The request could not be read. Check the format and size of what you sent."),
            JsonException => (400, "invalid_json", "The request body is not valid JSON or has values of the wrong type."),
            _ => (500, "internal_error", "Something went wrong on our side. Please try again; if it keeps happening, quote the correlation ID to support.")
        };
        var correlationId = context.TraceIdentifier; // set from X-Correlation-ID by CorrelationIdMiddleware
        if (status >= 500) logger.LogError(exception, "Unhandled exception for {Method} {Path} (correlation {CorrelationId})", context.Request.Method, context.Request.Path, correlationId);
        else logger.LogWarning(exception, "Request failed with {Status} {Code} for {Method} {Path} (correlation {CorrelationId})", status, code, context.Request.Method, context.Request.Path, correlationId);
        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
        problem.Extensions["correlationId"] = correlationId;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }
}
