using KosmosAdapterV2.Core.Models;

namespace KosmosAdapterV2.Core.Interfaces;

public interface IApiService
{
    Task<ApiResponse> UploadPhotoAsync(string customerId, string secret, string serviceDomain, byte[] imageData, CancellationToken cancellationToken = default);
    Task<ApiResponse> UploadFingerprintAsync(string customerId, string secret, string serviceDomain, byte[] fingerprintData, CancellationToken cancellationToken = default);
}
