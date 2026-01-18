using System.Text.Json.Serialization;

namespace PassManAPI.DTOs;

/// <summary>
/// Standardized error response format for all API errors.
/// Follows RFC 7807 Problem Details specification.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// A URI reference that identifies the problem type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "https://tools.ietf.org/html/rfc7231#section-6.5.1";

    /// <summary>
    /// A short, human-readable summary of the problem type.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP status code.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// A unique identifier for this particular occurrence of the problem.
    /// </summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// Validation errors, if any. Key is the field name, value is array of error messages.
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Creates a Bad Request (400) error response.
    /// </summary>
    public static ErrorResponse BadRequest(string detail, string? traceId = null)
    {
        return new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Bad Request",
            Status = 400,
            Detail = detail,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates a Validation Error (400) response with field-level errors.
    /// </summary>
    public static ErrorResponse ValidationError(Dictionary<string, string[]> errors, string? traceId = null)
    {
        return new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = 400,
            TraceId = traceId,
            Errors = errors
        };
    }

    /// <summary>
    /// Creates an Unauthorized (401) error response.
    /// </summary>
    public static ErrorResponse Unauthorized(string detail = "Authentication required.", string? traceId = null)
    {
        return new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            Title = "Unauthorized",
            Status = 401,
            Detail = detail,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates a Forbidden (403) error response.
    /// </summary>
    public static ErrorResponse Forbidden(string detail = "You do not have permission to access this resource.", string? traceId = null)
    {
        return new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            Title = "Forbidden",
            Status = 403,
            Detail = detail,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates a Not Found (404) error response.
    /// </summary>
    public static ErrorResponse NotFound(string detail = "The requested resource was not found.", string? traceId = null)
    {
        return new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "Not Found",
            Status = 404,
            Detail = detail,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates a Conflict (409) error response.
    /// </summary>
    public static ErrorResponse Conflict(string detail, string? traceId = null)
    {
        return new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            Title = "Conflict",
            Status = 409,
            Detail = detail,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates an Internal Server Error (500) response.
    /// </summary>
    public static ErrorResponse InternalServerError(string? detail = null, string? traceId = null)
    {
        return new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "Internal Server Error",
            Status = 500,
            Detail = detail ?? "An unexpected error occurred. Please try again later.",
            TraceId = traceId
        };
    }
}
