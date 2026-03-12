using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Core.Models;
using Microsoft.Extensions.Logging;

namespace KosmosAdapterV2.Infrastructure.Services;

public sealed class CameraService : ICameraService
{
    private readonly ILogger<CameraService> _logger;
    private readonly string _outputPath;
    private bool _isInitialized;
    private bool _disposed;

    public bool IsAvailable => _isInitialized;

    public CameraService(ILogger<CameraService> logger)
    {
        _logger = logger;
        _outputPath = Path.Combine("C:", "tmp", "Images");
        
        if (!Directory.Exists(_outputPath))
        {
            Directory.CreateDirectory(_outputPath);
        }
    }

    public bool Initialize()
    {
        try
        {
            _isInitialized = true;
            _logger.LogInformation("Camera service initialized");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize camera service");
            _isInitialized = false;
            return false;
        }
    }

    public async Task<BiometricResult> CapturePhotoAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            return BiometricResult.Fail("Camera service not initialized");
        }

        try
        {
            var fileName = $"photo_{sessionId}.jpg";
            var filePath = Path.Combine(_outputPath, fileName);

            _logger.LogInformation("Capturing photo for session {SessionId}", sessionId);

            await Task.Delay(100, cancellationToken);

            if (File.Exists(filePath))
            {
                var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
                using var image = Image.FromFile(filePath);
                
                return BiometricResult.Ok(filePath, data, new Bitmap(image));
            }

            return BiometricResult.Fail("Photo capture failed - no image received from camera");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Photo capture cancelled for session {SessionId}", sessionId);
            return BiometricResult.Fail("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing photo for session {SessionId}", sessionId);
            return BiometricResult.Fail(ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _isInitialized = false;
        _disposed = true;
        
        _logger.LogInformation("Camera service disposed");
    }
}
