using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core.ViewModel.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T Data { get; set; }
    public List<string> Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Not serialized — used by BaseApiController to set HTTP status code
    [JsonIgnore]
    public int StatusCode { get; set; } = 200;

    public static ApiResponse<T> SuccessResponse(T data, string message = "Operation successful")
        => new() { Success = true, Message = message, Data = data, StatusCode = 200 };

    public static ApiResponse<T> ErrorResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 200 };

    public static ApiResponse<T> NotFoundResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 404 };

    public static ApiResponse<T> UnauthorizedResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 401 };

    public static ApiResponse<T> ForbiddenResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 403 };

    public static ApiResponse<T> ConflictResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 409 };

    public static ApiResponse<T> ServerErrorResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 500 };
}
