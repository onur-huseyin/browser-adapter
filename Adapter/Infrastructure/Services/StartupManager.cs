using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using KosmosAdapterV2.Core.Interfaces;
using WinFormsApp = System.Windows.Forms.Application;

namespace KosmosAdapterV2.Infrastructure.Services;

public sealed class StartupManager : IStartupManager
{
    private const string StartupKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _appName;
    private readonly ILogger<StartupManager> _logger;

    public StartupManager(ILogger<StartupManager> logger)
    {
        _logger = logger;
        _appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "KosmosAdapterV2";
    }

    public bool IsStartupEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupKey);
                return key?.GetValue(_appName) != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public void EnableStartup()
    {
        try
        {
            var appPath = Environment.ProcessPath ?? WinFormsApp.ExecutablePath;
            
            using var key = Registry.CurrentUser.OpenSubKey(StartupKey, true);
            key?.SetValue(_appName, $"\"{appPath}\"");
            
            _logger.LogInformation("Startup enabled for {AppName}", _appName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable startup for {AppName}", _appName);
        }
    }

    public void DisableStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKey, true);
            key?.DeleteValue(_appName, false);
            
            _logger.LogInformation("Startup disabled for {AppName}", _appName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable startup for {AppName}", _appName);
        }
    }
}
