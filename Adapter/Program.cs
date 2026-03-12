using KosmosAdapterV2.Configuration;
using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Infrastructure.Services;
using KosmosAdapterV2.Infrastructure.Twain;
using KosmosAdapterV2.UI.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinFormsApp = System.Windows.Forms.Application;

namespace KosmosAdapterV2;

internal static class Program
{
    private static readonly Mutex AppMutex = new(false, "Global\\KosmosAdapterV2-4b4f575c-d585-45f0-a847-e567b19bda7a");

    /// <summary>
    /// kosmos2:// link ile açıldığında ilk açılışta işlenecek URL (pano dinlenmiyor, sadece protokol).
    /// </summary>
    public static string? PendingUrl { get; set; }

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--register-only", StringComparison.OrdinalIgnoreCase))
        {
            RegisterProtocolOnly();
            return;
        }

        var urlFromArgs = args.Length > 0 && args[0].StartsWith("kosmos2:", StringComparison.OrdinalIgnoreCase)
            ? args[0]
            : null;

        if (!AppMutex.WaitOne(0, false))
        {
            // kosmos2:// ile açıldıysa çalışan instance'a ilet; tarayıcı (tw/pt vb.) arayüzü orada açılır
            if (!string.IsNullOrEmpty(urlFromArgs))
            {
                if (ProtocolUrlPipe.SendUrlToExistingInstance(urlFromArgs))
                    return;
                // Pipe bağlanamadıysa (başka PC / zamanlama): URL'yi dosyaya yaz, instance FileSystemWatcher ile alıp ProcessUrl yapar
                ProtocolUrlPipe.WritePendingUrl(urlFromArgs);
                return;
            }
            MessageBox.Show("Uygulama zaten çalışıyor!", "Kosmos Adapter V2", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            PendingUrl = urlFromArgs;

            WinFormsApp.SetHighDpiMode(HighDpiMode.SystemAware);
            WinFormsApp.EnableVisualStyles();
            WinFormsApp.SetCompatibleTextRenderingDefault(false);

            if (!ValidateEnvironment())
                return;

            var configuration = ServiceCollectionExtensions.BuildConfiguration();
            var services = new ServiceCollection();
            services.AddKosmosServices(configuration);
            
            using var serviceProvider = services.BuildServiceProvider();
            
            var logger = serviceProvider.GetRequiredService<ILogger<MainTrayForm>>();
            logger.LogInformation("Kosmos Adapter V2 starting...");

            InitializeApplication(serviceProvider);

            var mainForm = serviceProvider.GetRequiredService<MainTrayForm>();
            WinFormsApp.Run(mainForm);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Uygulama başlatılamadı: {ex.Message}", 
                "Hata", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Error);
        }
        finally
        {
            AppMutex.ReleaseMutex();
        }
    }

    private static bool ValidateEnvironment()
    {
        if (TwainScannerService.ScreenBitDepth < 15)
        {
            MessageBox.Show(
                "Yüksek/gerçek renkli video modu gerekli!", 
                "Ekran Bit Derinliği", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Warning);
            return false;
        }

        var tempDirectory = Path.Combine(WinFormsApp.StartupPath, "temp");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var testFile = Path.Combine(tempDirectory, "test.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch
        {
            MessageBox.Show(
                $"\"{tempDirectory}\" klasörüne yazma izni vermelisiniz.", 
                "Hata", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Error);
            return false;
        }

        return true;
    }

    private static void InitializeApplication(IServiceProvider serviceProvider)
    {
        var protocolHandler = serviceProvider.GetRequiredService<IProtocolHandler>();
        protocolHandler.RegisterProtocol("kosmos2");

        var startupManager = serviceProvider.GetRequiredService<IStartupManager>();
        var appSettings = serviceProvider.GetRequiredService<AppSettings>();
        
        if (appSettings.EnableAutoStartup)
        {
            startupManager.EnableStartup();
        }

        EnsureDirectoriesExist(appSettings);
    }

    private static void EnsureDirectoriesExist(AppSettings settings)
    {
        var directories = new[]
        {
            settings.TempDirectory,
            settings.FingerprintDirectory,
            settings.ImageDirectory,
            ServiceCollectionExtensions.ResolveLogDirectory(settings.Logging.LogDirectory)
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }

    /// <summary>
    /// Sadece kosmos2 protokolünü kaydeder ve çıkar. Tarayıcıda link açılmıyorsa bu exe'yi "KosmosAdapterV2.exe --register-only" ile bir kez çalıştırın.
    /// </summary>
    private static void RegisterProtocolOnly()
    {
        try
        {
            var appPath = Environment.ProcessPath ?? WinFormsApp.ExecutablePath;
            if (string.IsNullOrEmpty(appPath))
            {
                MessageBox.Show("Uygulama yolu alınamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            RegisterProtocolInRegistry(appPath);
            MessageBox.Show(
                "kosmos2 protokolü kaydedildi.\n\nArtık tarayıcıda kosmos2:// linklerine tıklayabilirsiniz.",
                "Kosmos Adapter V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Protokol kaydedilemedi: " + ex.Message,
                "Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void RegisterProtocolInRegistry(string appPath)
    {
        const string protocolName = "kosmos2";
        appPath = appPath.Trim().TrimEnd('\\');
        var baseKey = $"HKCU\\Software\\Classes\\{protocolName}";

        try
        {
            RunReg("add", $"\"{baseKey}\"", "/ve", "/d", "\"URL: kosmos2 Protocol\"", "/f");
            RunReg("add", $"\"{baseKey}\"", "/v", "URL Protocol", "/t", "REG_SZ", "/d", "\"\"", "/f");
            var cmdVal = "\"\\\"" + appPath + "\\\" \\\"%1\\\"\"";
            RunReg("add", $"\"{baseKey}\\shell\\open\\command\"", "/ve", "/d", cmdVal, "/f");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("reg.exe ile kayıt yazılamadı: " + ex.Message);
        }
    }

    private static void RunReg(string verb, params string[] args)
    {
        var argLine = string.Join(" ", args);
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = verb + " " + argLine,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = System.Diagnostics.Process.Start(startInfo);
        p?.WaitForExit(5000);
        if (p != null && p.ExitCode != 0)
        {
            var err = p.StandardError.ReadToEnd();
            throw new InvalidOperationException($"reg exit {p.ExitCode}: {err}");
        }
    }
}
