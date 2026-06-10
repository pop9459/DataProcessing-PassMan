using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using PassManAPI.DTOs;

namespace PassManAPI.Middleware;

/// <summary>
/// Writes an <see cref="ErrorResponse"/> to the response, content-negotiated between XML and JSON
/// from the request's Accept header. Used by pipeline middleware (global exception handler, auth
/// 401/403) which run outside MVC's content negotiation and would otherwise always emit JSON.
/// </summary>
public static class ErrorResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly XmlSerializer XmlSerializer = new(typeof(ErrorResponse));

    public static async Task WriteAsync(HttpContext context, ErrorResponse error)
    {
        context.Response.StatusCode = error.Status;

        if (WantsXml(context.Request.Headers.Accept.ToString()))
        {
            context.Response.ContentType = "application/xml";
            using var buffer = new MemoryStream();
            XmlSerializer.Serialize(buffer, error);
            buffer.Position = 0;
            await buffer.CopyToAsync(context.Response.Body);
        }
        else
        {
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(error, JsonOptions);
        }
    }

    // Prefer XML only when the client explicitly asks for it and does not also accept JSON.
    private static bool WantsXml(string accept) =>
        (accept.Contains("application/xml", StringComparison.OrdinalIgnoreCase)
            || accept.Contains("text/xml", StringComparison.OrdinalIgnoreCase))
        && !accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
}
