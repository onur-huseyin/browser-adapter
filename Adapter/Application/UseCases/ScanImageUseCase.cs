using KosmosAdapterV2.Core.Enums;
using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Core.Models;
using Microsoft.Extensions.Logging;

namespace KosmosAdapterV2.Application.UseCases;

public interface IScanImageUseCase
{
    bool Initialize(IntPtr windowHandle);
    bool SelectSource();
    bool StartScan();
    TwainCommand ProcessMessage(ref Message message);
    IEnumerable<Bitmap> GetScannedImages();
    void CloseScan();
    event EventHandler<Bitmap>? ImageScanned;
}

public sealed class ScanImageUseCase : IScanImageUseCase, IDisposable
{
    private readonly IScannerService _scannerService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<ScanImageUseCase> _logger;
    private readonly string _tempPath;
    private bool _disposed;

    public event EventHandler<Bitmap>? ImageScanned;

    public ScanImageUseCase(
        IScannerService scannerService,
        IImageProcessingService imageProcessingService,
        ILogger<ScanImageUseCase> logger)
    {
        _scannerService = scannerService;
        _imageProcessingService = imageProcessingService;
        _logger = logger;
        _tempPath = Path.Combine("C:", "temp");

        if (!Directory.Exists(_tempPath))
        {
            Directory.CreateDirectory(_tempPath);
        }

        _scannerService.ImageScanned += OnImageScanned;
    }

    private void OnImageScanned(object? sender, Bitmap bitmap)
    {
        ImageScanned?.Invoke(this, bitmap);
    }

    public bool Initialize(IntPtr windowHandle)
    {
        _logger.LogInformation("Initializing scanner");
        return _scannerService.Initialize(windowHandle);
    }

    public bool SelectSource()
    {
        _logger.LogInformation("Selecting scanner source");
        return _scannerService.SelectSource();
    }

    public bool StartScan()
    {
        _logger.LogInformation("Starting scan");
        return _scannerService.StartScan();
    }

    public TwainCommand ProcessMessage(ref Message message)
    {
        return _scannerService.ProcessMessage(ref message);
    }

    public IEnumerable<Bitmap> GetScannedImages()
    {
        var images = _scannerService.TransferImages().ToList();
        
        foreach (var image in images)
        {
            var tempFilePath = Path.Combine(_tempPath, "ScanPass1_Pic1.jpg");
            _imageProcessingService.SaveImage(image, tempFilePath, ImageOutputFormat.Jpeg);
        }

        return images;
    }

    public void CloseScan()
    {
        _logger.LogInformation("Closing scanner");
        _scannerService.CloseScan();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _scannerService.ImageScanned -= OnImageScanned;
        _disposed = true;
    }
}
