using KosmosAdapterV2.Core.Models;

namespace KosmosAdapterV2.Core.Interfaces;

public interface ICameraService : IDisposable
{
    bool Initialize();
    Task<BiometricResult> CapturePhotoAsync(string sessionId, CancellationToken cancellationToken = default);
    bool IsAvailable { get; }
}
