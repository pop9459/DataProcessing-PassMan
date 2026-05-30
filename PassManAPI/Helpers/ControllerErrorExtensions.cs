using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PassManAPI.DTOs;

namespace PassManAPI.Helpers;

/// <summary>
/// Helpers for returning the standardized <see cref="ErrorResponse"/> (RFC7807 problem+json)
/// body from controllers, so every API error shares a single shape and always carries a
/// human-readable <c>detail</c> and a <c>traceId</c>. See the error-message audit (#162/#163).
/// </summary>
public static class ControllerErrorExtensions
{
    private static string TraceId(ControllerBase controller) =>
        Activity.Current?.Id ?? controller.HttpContext.TraceIdentifier;

    /// <summary>Returns the given <see cref="ErrorResponse"/> with its status code and problem+json content type.</summary>
    public static ObjectResult Error(this ControllerBase controller, ErrorResponse error) =>
        new(error)
        {
            StatusCode = error.Status,
            ContentTypes = { "application/problem+json" }
        };

    public static ObjectResult BadRequestProblem(this ControllerBase controller, string detail) =>
        controller.Error(ErrorResponse.BadRequest(detail, TraceId(controller)));

    public static ObjectResult NotFoundProblem(this ControllerBase controller, string detail = "The requested resource was not found.") =>
        controller.Error(ErrorResponse.NotFound(detail, TraceId(controller)));

    public static ObjectResult UnauthorizedProblem(this ControllerBase controller, string detail = "Authentication is required.") =>
        controller.Error(ErrorResponse.Unauthorized(detail, TraceId(controller)));

    public static ObjectResult ForbiddenProblem(this ControllerBase controller, string detail = "You do not have permission to access this resource.") =>
        controller.Error(ErrorResponse.Forbidden(detail, TraceId(controller)));

    public static ObjectResult ConflictProblem(this ControllerBase controller, string detail) =>
        controller.Error(ErrorResponse.Conflict(detail, TraceId(controller)));
}
