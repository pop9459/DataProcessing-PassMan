namespace PassManGUI.Models;

/// <summary>
/// Generic API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int? StatusCode { get; set; }

    public static ApiResponse<T> SuccessResponse(T data)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            StatusCode = 200
        };
    }

    public static ApiResponse<T> ErrorResponse(string errorMessage, int statusCode = 400)
    {
        return new ApiResponse<T>
        {
            Success = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode
        };
    }
}
