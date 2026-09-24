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
    // Machine-readable error code (e.g. "TRANSPORTATION_CONFLICT") for callers
    // that need to branch on the failure kind rather than parse Message. Null
    // for every existing response — additive, nothing else sets it.
    public string ErrorCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Not serialized — used by BaseApiController to set HTTP status code
    [JsonIgnore]
    public int StatusCode { get; set; } = 200;

    public static ApiResponse<T> SuccessResponse(T data, string message = "Operation successful")
        => new() { Success = true, Message = message, Data = data, StatusCode = 200 };

    /// <summary>A rejected request — failed validation or a rule the caller broke.
    /// 400, not 200: the frontend's response interceptor unwraps any 2xx as a
    /// success and hands the caller <c>data</c>, so returning 200 here made a
    /// validation failure look like a save that returned null.</summary>
    public static ApiResponse<T> ErrorResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 400 };

    /// <summary>Same, with a machine-readable code for callers that branch on the
    /// failure rather than display it.</summary>
    public static ApiResponse<T> ErrorResponse(string message, string errorCode, List<string> errors = null)
        => new() { Success = false, Message = message, ErrorCode = errorCode, Errors = errors ?? new(), StatusCode = 400 };

    public static ApiResponse<T> NotFoundResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 404 };

    public static ApiResponse<T> UnauthorizedResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 401 };

    public static ApiResponse<T> ForbiddenResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 403 };

    public static ApiResponse<T> ConflictResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 409 };

    public static ApiResponse<T> ConflictResponse(string message, string errorCode, List<string> errors = null)
        => new() { Success = false, Message = message, ErrorCode = errorCode, Errors = errors ?? new(), StatusCode = 409 };

    public static ApiResponse<T> ServerErrorResponse(string message, List<string> errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new(), StatusCode = 500 };
}
