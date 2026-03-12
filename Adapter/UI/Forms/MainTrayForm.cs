using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Core.Models;
using KosmosAdapterV2.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using WinFormsApp = System.Windows.Forms.Application;
using KosmosAdapterV2;

namespace KosmosAdapterV2.UI.Forms;

public partial class MainTrayForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainTrayForm> _logger;
    
    private NotifyIcon _notifyIcon = null!;
    private ContextMenuStrip _contextMenu = null!;
    private bool _isAvailable = true;
    private bool _isExiting;
    private CancellationTokenSource? _pipeCts;
    private FileSystemWatcher? _pendingUrlWatcher;

    public MainTrayForm(
        IServiceProvider serviceProvider,
        ILogger<MainTrayForm> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        InitializeComponent();
        InitializeTrayIcon();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1, 1);
        FormBorderStyle = FormBorderStyle.None;
        Name = "MainTrayForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-1000, -1000);
        Text = "Kosmos Adapter V2";
        WindowState = FormWindowState.Minimized;
        
        Load += MainTrayForm_Load;
        Shown += MainTrayForm_Shown;
        FormClosing += MainTrayForm_FormClosing;
        
        ResumeLayout(false);
    }

    private void InitializeTrayIcon()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Göster", null, OnShowClick);
        _contextMenu.Items.Add("Hakkında", null, OnAboutClick);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Çıkış", null, OnExitClick);

        Icon? appIcon = null;
        try
        {
            using var stream = typeof(MainTrayForm).Assembly.GetManifestResourceStream("KosmosAdapterV2.Resources.logo.png");
            if (stream != null)
            {
                using var bmp = new Bitmap(stream);
                appIcon = Icon.FromHandle(bmp.GetHicon());
            }
        }
        catch { }

        _notifyIcon = new NotifyIcon
        {
            Icon = appIcon ?? SystemIcons.Application,
            Text = "Kosmos Adapter V2 - Çalışıyor",
            Visible = true,
            ContextMenuStrip = _contextMenu,
            BalloonTipTitle = "✓ Kosmos Adapter V2 Çalışıyor (Sanalogi)",
            BalloonTipText = "Uygulama sistem tepsisinde aktif.\nkosmos2:// linkleri ile açılır.",
            BalloonTipIcon = ToolTipIcon.Info
        };

        _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;
        _notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
    }

    private void MainTrayForm_Load(object? sender, EventArgs e)
    {
        _logger.LogInformation("Main tray form loaded, kosmos2 protocol listener started");
        
        // kosmos2 link ile açıldıysa işle
        var pendingUrl = Program.PendingUrl;
        if (!string.IsNullOrEmpty(pendingUrl))
        {
            Program.PendingUrl = null;
            ProcessUrl(pendingUrl);
        }
        
        // Başka bir süreç kosmos2 link gönderirse dinle (uygulama zaten açıkken link tıklanırsa)
        _pipeCts = new CancellationTokenSource();
        ProtocolUrlPipe.RunServer(url =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                Invoke(() => ProcessUrl(url));
            }
            catch (ObjectDisposedException) { }
        }, _pipeCts.Token);

        // Pipe bağlanamadığında ikinci süreç URL'yi dosyaya yazar; burada yakalayıp tarayıcı arayüzünü açarız
        StartPendingUrlWatcher();
        
        // Windows sağ alt köşede bildirim göster (5 saniye)
        Task.Delay(500).ContinueWith(_ =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            Invoke(() =>
            {
                _notifyIcon?.ShowBalloonTip(5000, 
                    "✓ Kosmos Adapter V2 Çalışıyor", 
                    "Uygulama sistem tepsisinde aktif.\nkosmos2:// linkleri ile açılır.", 
                    ToolTipIcon.Info);
            });
        });
    }

    private void MainTrayForm_Shown(object? sender, EventArgs e)
    {
        Hide();
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        // Bildirime tıklanınca tarayıcı formunu aç
        ShowScannerForm();
    }

    private void ShowScannerForm()
    {
        var scannerForm = _serviceProvider.GetService(typeof(ImageEditorForm)) as ImageEditorForm;
        if (scannerForm != null)
        {
            scannerForm.ShowDialog(this);
        }
    }

    private void MainTrayForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isExiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon.ShowBalloonTip(1000, "Kosmos Adapter V2", "Uygulama arka planda çalışmaya devam ediyor.", ToolTipIcon.Info);
            return;
        }

        _pipeCts?.Cancel();
        _pipeCts?.Dispose();
        _pendingUrlWatcher?.Dispose();
        _pendingUrlWatcher = null;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    /// <summary>
    /// Pipe başarısız olunca ikinci süreç URL'yi LocalApplicationData\KosmosAdapterV2\pending_kosmos2.url yazar.
    /// </summary>
    private void StartPendingUrlWatcher()
    {
        var dir = ProtocolUrlPipe.PendingUrlDirectory;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); } catch { return; }
        }
        // Açılıştan hemen önce yazılmış dosya varsa işle
        var existing = ProtocolUrlPipe.ReadAndClearPendingUrl();
        if (!string.IsNullOrEmpty(existing))
            ProcessUrl(existing);

        try
        {
            _pendingUrlWatcher = new FileSystemWatcher(dir)
            {
                Filter = "pending_kosmos2.url",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            _pendingUrlWatcher.Created += OnPendingUrlFileChanged;
            _pendingUrlWatcher.Changed += OnPendingUrlFileChanged;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pending URL watcher başlatılamadı");
        }
    }

    private void OnPendingUrlFileChanged(object sender, FileSystemEventArgs e)
    {
        // Dosya yazımı bitene kadar kısa bekle
        Task.Delay(300).ContinueWith(_ =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            var url = ProtocolUrlPipe.ReadAndClearPendingUrl();
            if (!string.IsNullOrEmpty(url))
            {
                try { Invoke(() => ProcessUrl(url)); }
                catch (ObjectDisposedException) { }
            }
        });
    }

    private async void ProcessUrl(string url)
    {
        if (!_isAvailable) return;

        var request = KosmosRequest.Parse(url);
        var safeUrl = KosmosRequest.SanitizeUrlForLogging(url);

        // Her durumda tarayıcı ekranı açılır; URL geçersiz veya API için eksikse okuma/yükleme yapılmaz
        KosmosRequest? requestForForm = request is { IsValidForApi: true } ? request : null;

        if (request == null)
        {
            _logger.LogWarning(
                "Geçersiz veya yanlış kosmos2 linki: parse başarısız veya scheme kosmos2 değil. Form açılıyor, API okuma yapılmayacak. Url={Url}",
                safeUrl);
        }
        else if (!request.IsValidForApi)
        {
            var cidEmpty = string.IsNullOrWhiteSpace(request.CustomerId);
            var secretEmpty = string.IsNullOrWhiteSpace(request.Secret);
            _logger.LogWarning(
                "Eksik parametreli kosmos2 linki: API okuma/yükleme yapılmayacak. CidBoş={CidBoş} SecretBoş={SecretBoş} Type={Type} Domain={Domain} Path={Path}. Url={Url}",
                cidEmpty, secretEmpty, request.Type, request.ServiceDomain, request.BasePath, safeUrl);
        }
        else
        {
            _logger.LogInformation("Kosmos URL kabul: type={Type}, customerId={CustomerId}, domain={Domain}",
                request.Type, request.CustomerId, request.ServiceDomain);
        }

        try
        {
            _isAvailable = false;
            await ShowScannerFormAsync(requestForForm, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing URL");
            MessageBox.Show(
                $"Hata: {ex.Message}", 
                "Hata", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Error);
        }
        finally
        {
            _isAvailable = true;
        }
    }

    /// <param name="request">API okuma/yükleme için geçerliyse dolu; değilse null (sadece tarama/dosya aç)</param>
    /// <param name="rawUrl">Parse edilemeyen URL bilgilendirme için</param>
    private async Task ShowScannerFormAsync(KosmosRequest? request, string? rawUrl = null)
    {
        await Task.Run(() =>
        {
            Invoke(() =>
            {
                using var scannerForm = _serviceProvider.GetService(typeof(ImageEditorForm)) as ImageEditorForm;
                if (scannerForm != null)
                {
                    scannerForm.SetRequest(request, rawUrl);
                    scannerForm.ShowDialog(this);
                }
            });
        });
    }

    private void OnShowClick(object? sender, EventArgs e)
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OnAboutClick(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "Kosmos Adapter V2\n\nVersiyon: 2.0.0\n\n© 2026 Sanalogi",
            "Hakkında",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnExitClick(object? sender, EventArgs e)
    {
        _isExiting = true;
        WinFormsApp.Exit();
    }

    private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        OnShowClick(sender, e);
    }
}
