namespace KosmosAdapterV2.Core.Models;

public sealed class BiometricResult
{
    public bool Success { get; init; }
    public string? FilePath { get; init; }
    public byte[]? Data { get; init; }
    public string? Base64Data { get; init; }
    public string? ErrorMessage { get; init; }
    public Image? ProcessedImage { get; init; }

    public static BiometricResult Ok(string filePath, byte[]? data = null, Image? image = null) => new()
    {
        Success = true,
        FilePath = filePath,
        Data = data,
        ProcessedImage = image,
        Base64Data = data != null ? Convert.ToBase64String(data) : null
    };

    public static BiometricResult Fail(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}
