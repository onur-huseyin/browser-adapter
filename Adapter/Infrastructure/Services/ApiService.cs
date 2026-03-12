using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Core.Models;
using Microsoft.Extensions.Logging;

namespace KosmosAdapterV2.Infrastructure.Services;

public sealed class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResponse> UploadPhotoAsync(
        string customerId, 
        string secret, 
        string serviceDomain, 
        byte[] imageData, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var base64Image = $"data:image/jpeg;base64,{Convert.ToBase64String(imageData)}";
            
            // serviceDomain artık path de içerebilir: "domain.com/api" gibi
            // Eğer zaten /api içeriyorsa tekrar ekleme
            var baseUrl = serviceDomain.Contains("/api") 
                ? $"http://{serviceDomain}" 
                : $"http://{serviceDomain}/api";
            
            var url = $"{baseUrl}/Customers/{customerId}";
            
            _logger.LogInformation("Upload URL: {Url}, Image size: {Size} bytes", url, imageData.Length);
            
            return await SendPatchRequestAsync(url, secret, new { biometricPhotoPath = base64Image }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload photo for customer {CustomerId}", customerId);
            return ApiResponse.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse> UploadFingerprintAsync(
        string customerId, 
        string secret, 
        string serviceDomain, 
        byte[] fingerprintData, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var base64Data = $"data:file/eft;base64,{Convert.ToBase64String(fingerprintData)}";
            var url = $"http://{serviceDomain}/api/Customers/{customerId}";
            
            return await SendPatchRequestAsync(url, secret, new { biometricFingerPrintPath = base64Data }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload fingerprint for customer {CustomerId}", customerId);
            return ApiResponse.Fail(ex.Message);
        }
    }

    private async Task<ApiResponse> SendPatchRequestAsync<T>(
        string url, 
        string secret, 
        T payload, 
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        
        var json = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Sending PATCH request to {Url}", url);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Request successful: {StatusCode}", response.StatusCode);
            return ApiResponse.Ok("Upload successful");
        }

        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("Request failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
        
        return ApiResponse.Fail(errorContent, (int)response.StatusCode);
    }
}
