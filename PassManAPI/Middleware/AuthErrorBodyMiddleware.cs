using System.Diagnostics;
using PassManAPI.DTOs;

namespace PassManAPI.Middleware;

/// <summary>
/// Writes the standardized <see cref="ErrorResponse"/> body for 401/403 responses produced by the
/// authentication/authorization pipeline (challenge/forbid), which otherwise return an empty body.
/// Controller-returned 401/403 already carry a body, so those are left untouched. This runs as an
/// outer wrapper so it can inspect the final status code before the response is flushed — which is
/// why it works across the JWT + dev-header multi-scheme setup where JwtBearer events did not.
/// The body is content-negotiated (XML or JSON) via <see cref="ErrorResponseWriter"/>.
/// </summary>
public class AuthErrorBodyMiddleware
{
    private readonly RequestDelegate _next;

    public AuthErrorBodyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Only fill in a body the pipeline left empty (HasStarted/ContentType guard skips
        // controller responses that already wrote an ErrorResponse).
        if (context.Response.HasStarted ||
            !string.IsNullOrEmpty(context.Response.ContentType) ||
            (context.Response.ContentLength ?? 0) != 0)
        {
            return;
        }

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        ErrorResponse? error = context.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => ErrorResponse.Unauthorized(traceId: traceId),
            StatusCodes.Status403Forbidden => ErrorResponse.Forbidden(traceId: traceId),
            _ => null
        };

        if (error is not null)
        {
            await ErrorResponseWriter.WriteAsync(context, error);
        }
    }
}

public static class AuthErrorBodyMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthErrorBody(this IApplicationBuilder app) =>
        app.UseMiddleware<AuthErrorBodyMiddleware>();
}
