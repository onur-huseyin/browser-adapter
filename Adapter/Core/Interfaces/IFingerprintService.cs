using KosmosAdapterV2.Core.Models;
using KosmosAdapterV2.Core.Enums;

namespace KosmosAdapterV2.Core.Interfaces;

public interface IFingerprintService : IDisposable
{
    bool Initialize();
    Task<BiometricResult> CaptureFingerprints(string sessionId, CancellationToken cancellationToken = default);
    Task<BiometricResult> CreateNistFile(string sessionId, TransactionType transactionType, CancellationToken cancellationToken = default);
    bool IsAvailable { get; }
}
