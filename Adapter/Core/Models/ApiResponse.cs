namespace KosmosAdapterV2.Core.Models;

public sealed class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int StatusCode { get; init; }

    public static ApiResponse Ok(string? message = null) => new()
    {
        Success = true,
        Message = message,
        StatusCode = 200
    };

    public static ApiResponse Fail(string message, int statusCode = 500) => new()
    {
        Success = false,
        Message = message,
        StatusCode = statusCode
    };
}
