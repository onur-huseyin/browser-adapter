namespace KosmosAdapterV2.Configuration;

public sealed class AppSettings
{
    public string Version { get; set; } = "2.0.0";
    public string ProtocolName { get; set; } = "kosmos";
    public string TempDirectory { get; set; } = @"C:\temp";
    public string FingerprintDirectory { get; set; } = @"C:\tmp\Fingerprints";
    public string ImageDirectory { get; set; } = @"C:\tmp\Images";
    public bool EnableAutoStartup { get; set; } = true;
    public bool EnableAutoUpdate { get; set; } = true;
    public int DefaultImageWidth { get; set; } = 480;
    public int DefaultImageHeight { get; set; } = 640;
    public int FingerprintRejectQuality { get; set; } = 15;
    public UpdateSettings Update { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
}

public sealed class UpdateSettings
{
    public string VersionUrl { get; set; } = "http://s3.rancher-prod.kosmoslocal.local/kosmos-files/BioApp/version.json";
    public string DownloadUrl { get; set; } = "http://s3.rancher-prod.kosmoslocal.local/kosmos-files/BioApp/KosmosAdapterV2.zip";
}

public sealed class LoggingSettings
{
    public string LogDirectory { get; set; } = "logs";
    public string MinimumLevel { get; set; } = "Information";
    public int RetainedFileCountLimit { get; set; } = 30;
}
