namespace KosmosAdapterV2.Core.Interfaces;

public interface IClipboardMonitor : IDisposable
{
    void Start(IntPtr windowHandle);
    void Stop();
    event EventHandler<string>? ClipboardChanged;
}
