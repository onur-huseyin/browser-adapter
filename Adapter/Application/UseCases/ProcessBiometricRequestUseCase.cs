using KosmosAdapterV2.Core.Enums;
using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Core.Models;
using Microsoft.Extensions.Logging;

namespace KosmosAdapterV2.Application.UseCases;

public interface IProcessBiometricRequestUseCase
{
    Task<ApiResponse> ExecuteAsync(KosmosRequest request, CancellationToken cancellationToken = default);
}

public sealed class ProcessBiometricRequestUseCase : IProcessBiometricRequestUseCase
{
    private readonly IApiService _apiService;
    private readonly ICameraService _cameraService;
    private readonly IFingerprintService _fingerprintService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<ProcessBiometricRequestUseCase> _logger;

    public ProcessBiometricRequestUseCase(
        IApiService apiService,
        ICameraService cameraService,
        IFingerprintService fingerprintService,
        IImageProcessingService imageProcessingService,
        ILogger<ProcessBiometricRequestUseCase> logger)
    {
        _apiService = apiService;
        _cameraService = cameraService;
        _fingerprintService = fingerprintService;
        _imageProcessingService = imageProcessingService;
        _logger = logger;
    }

    public async Task<ApiResponse> ExecuteAsync(KosmosRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing biometric request for customer {CustomerId}, type {Type}", 
            request.CustomerId, request.Type);

        return request.Type switch
        {
            BiometricType.Photo => await ProcessPhotoRequestAsync(request, cancellationToken),
            BiometricType.Fingerprint => await ProcessFingerprintRequestAsync(request, cancellationToken),
            BiometricType.TwainScan => ApiResponse.Ok("Twain scan requires UI interaction"),
            _ => ApiResponse.Fail("Unknown biometric type")
        };
    }

    private async Task<ApiResponse> ProcessPhotoRequestAsync(KosmosRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!_cameraService.IsAvailable)
            {
                _cameraService.Initialize();
            }

            var sessionId = Guid.NewGuid().ToString();
            var result = await _cameraService.CapturePhotoAsync(sessionId, cancellationToken);

            if (!result.Success || result.Data == null)
            {
                return ApiResponse.Fail(result.ErrorMessage ?? "Photo capture failed");
            }

            return await _apiService.UploadPhotoAsync(
                request.CustomerId,
                request.Secret,
                request.ServiceDomain,
                result.Data,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing photo request");
            return ApiResponse.Fail(ex.Message);
        }
    }

    private async Task<ApiResponse> ProcessFingerprintRequestAsync(KosmosRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!_fingerprintService.IsAvailable)
            {
                _fingerprintService.Initialize();
            }

            var sessionId = Guid.NewGuid().ToString();
            var result = await _fingerprintService.CaptureFingerprints(sessionId, cancellationToken);

            if (!result.Success || result.Data == null)
            {
                return ApiResponse.Fail(result.ErrorMessage ?? "Fingerprint capture failed");
            }

            return await _apiService.UploadFingerprintAsync(
                request.CustomerId,
                request.Secret,
                request.ServiceDomain,
                result.Data,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fingerprint request");
            return ApiResponse.Fail(ex.Message);
        }
    }
}
