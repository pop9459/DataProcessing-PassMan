using System.Text.Json.Serialization;
using System.Xml.Serialization;

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
    [XmlIgnore] // A Dictionary isn't XML-serializable; XML uses the list view below.
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// XML-serializable view of <see cref="Errors"/>. JSON emits the <c>errors</c> object above;
    /// XML can't serialize a dictionary, so the same field errors are exposed here as a list so that
    /// error bodies carry validation details in <b>both</b> formats.
    /// </summary>
    [JsonIgnore]
    [XmlArray("errors")]
    [XmlArrayItem("error")]
    public List<ValidationError>? ValidationErrors
    {
        get => Errors?.Select(kv => new ValidationError { Field = kv.Key, Messages = kv.Value }).ToList();
        set => Errors = value?.ToDictionary(v => v.Field, v => v.Messages);
    }

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
    /// Creates a Locked (423) error response (e.g. account lockout).
    /// </summary>
    public static ErrorResponse Locked(string detail, string? traceId = null)
    {
        return new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc4918#section-11.3",
            Title = "Locked",
            Status = 423,
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

/// <summary>
/// XML-serializable representation of a single field's validation errors
/// (the dictionary form used for JSON isn't XML-serializable).
/// </summary>
public class ValidationError
{
    [XmlAttribute("field")]
    public string Field { get; set; } = string.Empty;

    [XmlElement("message")]
    public string[] Messages { get; set; } = Array.Empty<string>();
}
