using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace RAGGit.Workstation.Api.Middleware;

/// <summary>
/// Ensures every request carries an <c>X-Request-Id</c> correlation id.
/// If the caller supplies one in the header it is reused; otherwise a
/// new GUID is generated. The value is pushed into the Serilog log context
/// so every log entry emitted during the request is tagged with
/// <c>RequestId</c>.
/// </summary>
public sealed class RequestIdMiddleware
{
    internal const string HeaderName = "X-Request-Id";

    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
            context.Response.Headers[HeaderName] = requestId;
        }

        context.Items[HeaderName] = requestId;

        using (LogContext.PushProperty("RequestId", requestId))
        {
            await _next(context);
        }
    }
}
