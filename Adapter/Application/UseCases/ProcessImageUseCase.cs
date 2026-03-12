using KosmosAdapterV2.Core.Enums;
using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Core.Models;
using Microsoft.Extensions.Logging;

namespace KosmosAdapterV2.Application.UseCases;

public interface IProcessImageUseCase
{
    Bitmap Crop(Bitmap source, CropRegion region);
    Bitmap Rotate(Bitmap source, float angle);
    Bitmap Resize(Bitmap source, int width, int height);
    Task<ApiResponse> UploadImageAsync(KosmosRequest request, Image image, CancellationToken cancellationToken = default);
}

public sealed class ProcessImageUseCase : IProcessImageUseCase
{
    private readonly IApiService _apiService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<ProcessImageUseCase> _logger;

    private const int DefaultWidth = 480;
    private const int DefaultHeight = 640;

    public ProcessImageUseCase(
        IApiService apiService,
        IImageProcessingService imageProcessingService,
        ILogger<ProcessImageUseCase> logger)
    {
        _apiService = apiService;
        _imageProcessingService = imageProcessingService;
        _logger = logger;
    }

    public Bitmap Crop(Bitmap source, CropRegion region)
    {
        _logger.LogDebug("Cropping image to region {Region}", region);
        return _imageProcessingService.Crop(source, region);
    }

    public Bitmap Rotate(Bitmap source, float angle)
    {
        _logger.LogDebug("Rotating image by {Angle} degrees", angle);
        return _imageProcessingService.Rotate(source, angle);
    }

    public Bitmap Resize(Bitmap source, int width, int height)
    {
        _logger.LogDebug("Resizing image to {Width}x{Height}", width, height);
        return _imageProcessingService.Resize(source, width, height);
    }

    public async Task<ApiResponse> UploadImageAsync(KosmosRequest request, Image image, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Uploading image for customer {CustomerId} to {BaseUrl}", 
                request.CustomerId, request.GetApiBaseUrl());

            using var resized = _imageProcessingService.Resize((Bitmap)image, DefaultWidth, DefaultHeight);
            var imageData = _imageProcessingService.ToByteArray(resized, ImageOutputFormat.Jpeg);

            // ServiceDomain yerine tam base URL kullan (path dahil)
            var baseUrlWithPath = $"{request.ServiceDomain}{request.BasePath}";

            return await _apiService.UploadPhotoAsync(
                request.CustomerId,
                request.Secret,
                baseUrlWithPath,
                imageData,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return ApiResponse.Fail(ex.Message);
        }
    }
}
