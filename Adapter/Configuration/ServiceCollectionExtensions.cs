using KosmosAdapterV2.Application.UseCases;
using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Infrastructure.Services;
using KosmosAdapterV2.Infrastructure.Twain;
using KosmosAdapterV2.UI.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace KosmosAdapterV2.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKosmosServices(this IServiceCollection services, IConfiguration configuration)
    {
        var appSettings = configuration.Get<AppSettings>() ?? new AppSettings();
        services.AddSingleton(appSettings);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();

            // Windows: tüm loglar .txt dosyasına; göreli yol exe klasörüne sabitlenir
            var logDir = ResolveLogDirectory(appSettings.Logging.LogDirectory);
            Directory.CreateDirectory(logDir);
            var logFilePath = Path.Combine(logDir, "kosmos-.txt");

            var logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: appSettings.Logging.RetainedFileCountLimit,
                    shared: true)
                .CreateLogger();

            builder.AddSerilog(logger, dispose: true);
        });

        services.AddHttpClient<IApiService, ApiService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IScannerService, TwainScannerService>();
        services.AddSingleton<IImageProcessingService, ImageProcessingService>();
        services.AddSingleton<ICameraService, CameraService>();
        services.AddSingleton<IFingerprintService, FingerprintService>();
        services.AddSingleton<IProtocolHandler, ProtocolHandler>();
        services.AddSingleton<IStartupManager, StartupManager>();

        services.AddTransient<IProcessBiometricRequestUseCase, ProcessBiometricRequestUseCase>();
        services.AddTransient<IProcessImageUseCase, ProcessImageUseCase>();
        services.AddTransient<IScanImageUseCase, ScanImageUseCase>();

        services.AddTransient<MainTrayForm>();
        services.AddTransient<ImageEditorForm>();
        services.AddTransient<ImageInfoForm>();

        return services;
    }

    /// <summary>
    /// Log klasörü göreliyse exe yanına alınır (Windows publish senaryosu).
    /// </summary>
    public static string ResolveLogDirectory(string? logDirectory)
    {
        var d = string.IsNullOrWhiteSpace(logDirectory) ? "logs" : logDirectory.Trim();
        if (!Path.IsPathRooted(d))
            d = Path.Combine(AppContext.BaseDirectory, d);
        return Path.GetFullPath(d);
    }

    public static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
    }
}
