using KosmosAdapterV2.Core.Enums;
using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Core.Models;
using Microsoft.Extensions.Logging;

namespace KosmosAdapterV2.Infrastructure.Services;

public sealed class FingerprintService : IFingerprintService
{
    private readonly ILogger<FingerprintService> _logger;
    private readonly string _outputPath;
    private readonly string _tempFolder;
    private bool _isInitialized;
    private bool _disposed;

    public bool IsAvailable => _isInitialized;

    public FingerprintService(ILogger<FingerprintService> logger)
    {
        _logger = logger;
        _outputPath = Path.Combine("C:", "tmp", "Fingerprints");
        _tempFolder = Path.Combine(_outputPath, "Temp");

        if (!Directory.Exists(_tempFolder))
        {
            Directory.CreateDirectory(_tempFolder);
        }
    }

    public bool Initialize()
    {
        try
        {
            _isInitialized = true;
            _logger.LogInformation("Fingerprint service initialized");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize fingerprint service");
            _isInitialized = false;
            return false;
        }
    }

    public async Task<BiometricResult> CaptureFingerprints(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            return BiometricResult.Fail("Fingerprint service not initialized");
        }

        try
        {
            _logger.LogInformation("Capturing fingerprints for session {SessionId}", sessionId);

            await Task.Delay(100, cancellationToken);

            var atpResult = await CreateNistFile(sessionId, TransactionType.ATP, cancellationToken);
            if (!atpResult.Success)
            {
                return atpResult;
            }

            var avtResult = await CreateNistFile(sessionId, TransactionType.AVT, cancellationToken);
            if (!avtResult.Success)
            {
                return avtResult;
            }

            return atpResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Fingerprint capture cancelled for session {SessionId}", sessionId);
            return BiometricResult.Fail("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing fingerprints for session {SessionId}", sessionId);
            return BiometricResult.Fail(ex.Message);
        }
    }

    public async Task<BiometricResult> CreateNistFile(string sessionId, TransactionType transactionType, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileStartString = $"fp_{sessionId}";
            var suffix = transactionType == TransactionType.AVT ? "_AVT" : "";
            var fileName = $"{fileStartString}{suffix}.eft";
            var filePath = Path.Combine(_outputPath, fileName);

            _logger.LogInformation("Creating NIST file for session {SessionId}, type {TransactionType}", sessionId, transactionType);

            await Task.Delay(50, cancellationToken);

            if (File.Exists(filePath))
            {
                var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
                return BiometricResult.Ok(filePath, data);
            }

            return BiometricResult.Fail($"NIST file creation failed for {transactionType}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating NIST file for session {SessionId}", sessionId);
            return BiometricResult.Fail(ex.Message);
        }
    }

    private void DeleteTempFolder()
    {
        try
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temp folder");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        DeleteTempFolder();
        _isInitialized = false;
        _disposed = true;

        _logger.LogInformation("Fingerprint service disposed");
    }
}
