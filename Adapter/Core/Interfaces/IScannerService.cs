using KosmosAdapterV2.Core.Models;
using KosmosAdapterV2.Core.Enums;

namespace KosmosAdapterV2.Core.Interfaces;

public interface IScannerService : IDisposable
{
    bool Initialize(IntPtr windowHandle);
    bool SelectSource();
    bool StartScan();
    TwainCommand ProcessMessage(ref Message message);
    IEnumerable<Bitmap> TransferImages();
    void CloseScan();
    event EventHandler<Bitmap>? ImageScanned;
}
