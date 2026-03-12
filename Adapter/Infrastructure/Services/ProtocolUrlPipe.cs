using System.IO.Pipes;
using System.Text;

namespace KosmosAdapterV2.Infrastructure.Services;

public static class ProtocolUrlPipe
{
    public const string PipeName = "KosmosAdapterV2.URLPipe";
    private const string PendingFileName = "pending_kosmos2.url";

    private static string PendingFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KosmosAdapterV2",
            PendingFileName);

    /// <summary>
    /// Pipe başarısız olursa çalışan instance FileSystemWatcher ile bu dosyayı okuyup ProcessUrl çağırır.
    /// </summary>
    public static void WritePendingUrl(string url)
    {
        try
        {
            var dir = Path.GetDirectoryName(PendingFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var tmp = PendingFilePath + ".tmp";
            File.WriteAllText(tmp, url, Encoding.UTF8);
            File.Move(tmp, PendingFilePath, overwrite: true);
        }
        catch
        {
            // ignore
        }
    }

    public static string? ReadAndClearPendingUrl()
    {
        try
        {
            if (!File.Exists(PendingFilePath))
                return null;
            var url = File.ReadAllText(PendingFilePath, Encoding.UTF8).Trim();
            try { File.Delete(PendingFilePath); } catch { }
            return string.IsNullOrEmpty(url) ? null : url;
        }
        catch
        {
            return null;
        }
    }

    public static string PendingUrlDirectory =>
        Path.GetDirectoryName(PendingFilePath) ?? "";

    public static bool SendUrlToExistingInstance(string url) =>
        SendUrlToExistingInstance(url, attempts: 8, connectTimeoutMs: 3000);

    public static bool SendUrlToExistingInstance(string url, int attempts, int connectTimeoutMs)
    {
        for (var i = 0; i < attempts; i++)
        {
            if (TrySendOnce(url, connectTimeoutMs))
                return true;
            Thread.Sleep(400);
        }
        return false;
    }

    private static bool TrySendOnce(string url, int connectTimeoutMs)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(connectTimeoutMs);
            var bytes = Encoding.UTF8.GetBytes(url + "\n");
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void RunServer(Action<string> onUrlReceived, CancellationToken cancel = default)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte);
                    server.WaitForConnection();
                    if (cancel.IsCancellationRequested) break;

                    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: false);
                    var line = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        onUrlReceived(line);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    // Client disconnected, continue listening
                }
                catch (Exception)
                {
                    Thread.Sleep(500);
                }
            }
        });
    }
}
