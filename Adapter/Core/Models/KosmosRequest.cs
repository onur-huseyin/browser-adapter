using System.Text.RegularExpressions;
using KosmosAdapterV2.Core.Enums;

namespace KosmosAdapterV2.Core.Models;

public sealed class KosmosRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public string Secret { get; init; } = string.Empty;
    public string ServiceDomain { get; init; } = string.Empty;
    public string BasePath { get; init; } = string.Empty;
    public BiometricType Type { get; init; }
    public string RawUrl { get; init; } = string.Empty;

    /// <summary>
    /// API'ye yükleme / otomatik okuma için URL yeterli mi (eksik veya farklı parametrelerde false).
    /// </summary>
    public bool IsValidForApi =>
        !string.IsNullOrWhiteSpace(CustomerId) && !string.IsNullOrWhiteSpace(Secret);

    /// <summary>
    /// API endpoint için tam base URL döndürür
    /// Örnek: http://backoffice-api.rancher-test.kosmoslocal.local/api
    /// </summary>
    public string GetApiBaseUrl() => $"http://{ServiceDomain}{BasePath}".TrimEnd('/');

    /// <summary>
    /// Loglara yazarken secret parametresini maskele (JWT/token sızmasın).
    /// </summary>
    public static string SanitizeUrlForLogging(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "(boş)";
        try
        {
            var masked = Regex.Replace(url, @"([?&]secret=)[^&]*", "$1***", RegexOptions.IgnoreCase);
            if (masked.Length > 500)
                return masked[..500] + "...(kısaltıldı)";
            return masked;
        }
        catch
        {
            return url.Length > 200 ? url[..200] + "..." : url;
        }
    }

    public static KosmosRequest? Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var uri = new Uri(url);
            
            if (!uri.Scheme.Equals("kosmos2", StringComparison.OrdinalIgnoreCase))
                return null;

            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var customerId = query.Get("cid") ?? string.Empty;
            var secret = query.Get("secret") ?? string.Empty;
            var typeStr = query.Get("type") ?? string.Empty;

            var type = typeStr.ToLowerInvariant() switch
            {
                "tw" => BiometricType.TwainScan,
                "pt" => BiometricType.Photo,
                "fp" => BiometricType.Fingerprint,
                _ => BiometricType.TwainScan
            };

            // Path'i al ve trailing slash'ı temizle
            var basePath = uri.AbsolutePath.TrimEnd('/');

            return new KosmosRequest
            {
                CustomerId = customerId,
                Secret = secret,
                ServiceDomain = uri.Authority,
                BasePath = basePath,
                Type = type,
                RawUrl = url
            };
        }
        catch
        {
            return null;
        }
    }
}
