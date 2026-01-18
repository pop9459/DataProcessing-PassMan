using System.Diagnostics;
using System.Net;
using System.Text.Json;
using PassManAPI.DTOs;

namespace PassManAPI.Middleware;

/// <summary>
/// Global exception handler middleware that catches unhandled exceptions
/// and returns standardized error responses.
/// </summary>
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        _logger.LogError(exception, "An unhandled exception occurred. TraceId: {TraceId}", traceId);

        var response = exception switch
        {
            ArgumentNullException argNullEx => ErrorResponse.BadRequest(
                $"A required argument was not provided: {argNullEx.ParamName}", traceId),

            ArgumentException argEx => ErrorResponse.BadRequest(
                argEx.Message, traceId),

            UnauthorizedAccessException => ErrorResponse.Unauthorized(
                "You are not authorized to perform this action.", traceId),

            KeyNotFoundException => ErrorResponse.NotFound(
                "The requested resource was not found.", traceId),

            InvalidOperationException invalidOpEx => ErrorResponse.BadRequest(
                invalidOpEx.Message, traceId),

            NotSupportedException notSupportedEx => ErrorResponse.BadRequest(
                notSupportedEx.Message, traceId),

            TimeoutException => new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.5",
                Title = "Gateway Timeout",
                Status = 504,
                Detail = "The request timed out. Please try again later.",
                TraceId = traceId
            },

            _ => CreateInternalServerError(exception, traceId)
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = response.Status;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsJsonAsync(response, options);
    }

    private ErrorResponse CreateInternalServerError(Exception exception, string traceId)
    {
        // In development, include exception details
        if (_environment.IsDevelopment())
        {
            return new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal Server Error",
                Status = 500,
                Detail = $"{exception.GetType().Name}: {exception.Message}",
                TraceId = traceId
            };
        }

        // In production, hide exception details
        return ErrorResponse.InternalServerError(traceId: traceId);
    }
}

/// <summary>
/// Extension methods for registering the exception handler middleware.
/// </summary>
public static class ExceptionHandlerMiddlewareExtensions
{
    /// <summary>
    /// Adds the global exception handler middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlerMiddleware>();
    }
}
