using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using KosmosAdapterV2.Core.Interfaces;
using WinFormsApp = System.Windows.Forms.Application;

namespace KosmosAdapterV2.Infrastructure.Services;

public sealed class ProtocolHandler : IProtocolHandler
{
    private readonly ILogger<ProtocolHandler> _logger;

    public ProtocolHandler(ILogger<ProtocolHandler> logger)
    {
        _logger = logger;
    }

    private const string ClassesPath = @"Software\Classes";

    public void RegisterProtocol(string protocolName)
    {
        try
        {
            var appPath = Environment.ProcessPath ?? WinFormsApp.ExecutablePath;
            if (string.IsNullOrEmpty(appPath))
                return;
            // HKEY_CURRENT_USER kullan - yönetici izni gerekmez
            using var classesKey = Registry.CurrentUser.CreateSubKey(ClassesPath, true);
            if (classesKey == null)
                return;
            var protocolPath = protocolName;
            try
            {
                using var existing = classesKey.OpenSubKey(protocolPath);
                if (existing != null)
                {
                    classesKey.DeleteSubKeyTree(protocolPath);
                    _logger?.LogInformation("Existing protocol registration removed: {Protocol}", protocolName);
                }
            }
            catch { }
            using var newKey = classesKey.CreateSubKey(protocolPath);
            if (newKey != null)
            {
                newKey.SetValue(string.Empty, $"URL: {protocolName} Protocol");
                newKey.SetValue("URL Protocol", string.Empty);
                using var commandKey = newKey.CreateSubKey(@"shell\open\command");
                commandKey.SetValue(string.Empty, $"\"{appPath}\" \"%1\"");
                _logger?.LogInformation("Protocol registered: {Protocol} -> {AppPath}", protocolName, appPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to register protocol: {Protocol}", protocolName);
        }
    }

    public bool IsProtocolRegistered(string protocolName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ClassesPath + "\\" + protocolName);
            return key != null;
        }
        catch
        {
            return false;
        }
    }
}
