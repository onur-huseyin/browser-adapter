using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using KosmosAdapterV2.Configuration;

namespace KosmosAdapterV2.Infrastructure.Services;

public sealed class AutoUpdateService
{
    private readonly AppSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly string _appDirectory;
    private readonly string _localVersionFile;
    private const string DefaultVersion = "1.0.0";

    public AutoUpdateService(AppSettings settings)
    {
        _settings = settings;
        
        // HTTP ve HTTPS desteği için handler
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
        
        _appDirectory = AppContext.BaseDirectory;
        _localVersionFile = Path.Combine(_appDirectory, "version.local.json");
        
        // İlk çalıştırmada local version dosyası yoksa oluştur
        EnsureLocalVersionFile();
    }

    public string GetLocalVersion()
    {
        try
        {
            if (File.Exists(_localVersionFile))
            {
                var json = File.ReadAllText(_localVersionFile);
                var versionInfo = JsonSerializer.Deserialize<LocalVersionInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                // Versiyon geçerli mi kontrol et
                if (versionInfo != null && IsValidVersion(versionInfo.Version))
                {
                    return versionInfo.Version;
                }
                
                // Bozuk dosya, sil ve yeniden oluştur
                RepairLocalVersionFile();
            }
        }
        catch
        {
            // Parse hatası, dosya bozuk - sil ve yeniden oluştur
            RepairLocalVersionFile();
        }
        
        return _settings.Version;
    }

    public void SaveLocalVersion(string version)
    {
        try
        {
            var versionInfo = new LocalVersionInfo
            {
                Version = version,
                UpdatedAt = DateTime.Now.ToString("o")
            };
            var json = JsonSerializer.Serialize(versionInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_localVersionFile, json);
        }
        catch { }
    }

    private void EnsureLocalVersionFile()
    {
        if (!File.Exists(_localVersionFile))
        {
            // appsettings.json'daki versiyonu kullan (ilk kurulum)
            SaveLocalVersion(_settings.Version);
        }
        else
        {
            // Dosya var ama geçerli mi kontrol et
            ValidateAndRepairLocalVersionFile();
        }
    }

    private void ValidateAndRepairLocalVersionFile()
    {
        try
        {
            var json = File.ReadAllText(_localVersionFile);
            var versionInfo = JsonSerializer.Deserialize<LocalVersionInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (versionInfo == null || !IsValidVersion(versionInfo.Version))
            {
                RepairLocalVersionFile();
            }
        }
        catch
        {
            RepairLocalVersionFile();
        }
    }

    private void RepairLocalVersionFile()
    {
        try
        {
            // Bozuk dosyayı sil
            if (File.Exists(_localVersionFile))
            {
                File.Delete(_localVersionFile);
            }
            
            // Yeni dosya oluştur
            SaveLocalVersion(_settings.Version);
        }
        catch { }
    }

    private bool IsValidVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        return Version.TryParse(version, out _);
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(_settings.Update.VersionUrl);
            var remoteInfo = JsonSerializer.Deserialize<VersionInfo>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (remoteInfo == null)
                return new UpdateCheckResult { HasUpdate = false };

            var localVersionStr = GetLocalVersion();
            var remoteVersion = Version.Parse(remoteInfo.Version);
            var localVersion = Version.Parse(localVersionStr);

            return new UpdateCheckResult
            {
                HasUpdate = remoteVersion > localVersion,
                CurrentVersion = localVersionStr,
                NewVersion = remoteInfo.Version,
                DownloadUrl = remoteInfo.DownloadUrl ?? _settings.Update.DownloadUrl,
                ReleaseNotes = remoteInfo.ReleaseNotes
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                HasUpdate = false,
                Error = ex.Message
            };
        }
    }

    public async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, string newVersion, IProgress<int>? progress = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KosmosAdapterV2_Update");
        var zipPath = Path.Combine(tempDir, "update.zip");
        var extractPath = Path.Combine(tempDir, "extracted");
        var updaterPath = Path.Combine(tempDir, "updater.bat");

        try
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(extractPath);

            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var downloadedBytes = 0L;

                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percentage = (int)((downloadedBytes * 100) / totalBytes);
                        progress?.Report(percentage);
                    }
                }
            }

            ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            // Yeni versiyon bilgisini hazırla (güncelleme sonrası yazılacak)
            var newVersionJson = JsonSerializer.Serialize(new LocalVersionInfo
            {
                Version = newVersion,
                UpdatedAt = DateTime.Now.ToString("o")
            }, new JsonSerializerOptions { WriteIndented = true });
            var versionFileInExtract = Path.Combine(extractPath, "version.local.json");
            await File.WriteAllTextAsync(versionFileInExtract, newVersionJson);

            var updaterScript = $@"@echo off
chcp 65001 >nul
echo Kosmos Adapter V2 güncelleniyor...
timeout /t 2 /nobreak >nul

:waitloop
tasklist /FI ""IMAGENAME eq KosmosAdapterV2.exe"" 2>NUL | find /I /N ""KosmosAdapterV2.exe"">NUL
if ""%ERRORLEVEL%""==""0"" (
    echo Uygulama kapanması bekleniyor...
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo Dosyalar kopyalanıyor...
xcopy /E /Y /Q ""{extractPath}\*"" ""{_appDirectory}""

echo Uygulama yeniden başlatılıyor...
start """" ""{Path.Combine(_appDirectory, "KosmosAdapterV2.exe")}""

echo Temizlik yapılıyor...
timeout /t 2 /nobreak >nul
rd /s /q ""{tempDir}""
exit
";

            await File.WriteAllTextAsync(updaterPath, updaterScript, System.Text.Encoding.UTF8);

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{updaterPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
            return false;
        }
    }

    public string? LastError { get; private set; }
}

public class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public string? CurrentVersion { get; set; }
    public string? NewVersion { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? Error { get; set; }
}

public class VersionInfo
{
    public string Version { get; set; } = "";
    public string? DownloadUrl { get; set; }
    public string? ReleaseNotes { get; set; }
}

public class LocalVersionInfo
{
    public string Version { get; set; } = "";
    public string? UpdatedAt { get; set; }
}
