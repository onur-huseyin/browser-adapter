using System.Runtime.InteropServices;
using KosmosAdapterV2.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace KosmosAdapterV2.Infrastructure.Services;

public sealed class ClipboardMonitor : IClipboardMonitor
{
    private readonly ILogger<ClipboardMonitor> _logger;
    private IntPtr _nextClipboardViewer;
    private IntPtr _windowHandle;
    private bool _isRunning;

    public event EventHandler<string>? ClipboardChanged;

    [DllImport("User32.dll")]
    private static extern int SetClipboardViewer(int hWndNewViewer);

    [DllImport("User32.dll", CharSet = CharSet.Auto)]
    private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

    public ClipboardMonitor(ILogger<ClipboardMonitor> logger)
    {
        _logger = logger;
    }

    public void Start(IntPtr windowHandle)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Clipboard monitor is already running");
            return;
        }

        _windowHandle = windowHandle;
        _nextClipboardViewer = (IntPtr)SetClipboardViewer((int)windowHandle);
        _isRunning = true;
        
        _logger.LogInformation("Clipboard monitor started");
    }

    public void Stop()
    {
        if (!_isRunning) return;

        ChangeClipboardChain(_windowHandle, _nextClipboardViewer);
        _isRunning = false;
        
        _logger.LogInformation("Clipboard monitor stopped");
    }

    public void ProcessClipboardMessage()
    {
        try
        {
            if (!_isRunning) return;

            var dataObject = Clipboard.GetDataObject();
            var text = dataObject?.GetData(DataFormats.Text) as string;

            if (!string.IsNullOrEmpty(text))
            {
                ClipboardChanged?.Invoke(this, text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing clipboard data");
        }
    }

    public void HandleChainChange(IntPtr wParam, IntPtr lParam)
    {
        if (wParam == _nextClipboardViewer)
        {
            _nextClipboardViewer = lParam;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
