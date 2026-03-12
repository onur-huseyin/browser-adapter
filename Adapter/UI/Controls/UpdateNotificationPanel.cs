using KosmosAdapterV2.Infrastructure.Services;

namespace KosmosAdapterV2.UI.Controls;

public sealed class UpdateNotificationPanel : Panel
{
    private readonly AutoUpdateService _updateService;
    private UpdateCheckResult? _updateInfo;
    
    private Label _lblMessage = null!;
    private Button _btnUpdate = null!;
    private Button _btnClose = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblProgress = null!;
    private bool _isUpdating;

    public event EventHandler? UpdateStarted;
    public event EventHandler? PanelClosed;

    public UpdateNotificationPanel(AutoUpdateService updateService)
    {
        _updateService = updateService;
        InitializeComponent();
        Visible = false;
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Size = new Size(320, 70);
        BackColor = Color.FromArgb(0, 100, 180);
        Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Padding = new Padding(10);

        _lblMessage = new Label
        {
            Text = "Yeni güncelleme mevcut!",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(10, 8),
            Size = new Size(250, 18),
            AutoSize = false
        };

        _btnUpdate = new Button
        {
            Text = "Güncelle",
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            BackColor = Color.FromArgb(50, 180, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(10, 32),
            Size = new Size(80, 28),
            Cursor = Cursors.Hand
        };
        _btnUpdate.FlatAppearance.BorderSize = 0;
        _btnUpdate.Click += BtnUpdate_Click;

        _btnClose = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(290, 5),
            Size = new Size(24, 24),
            Cursor = Cursors.Hand
        };
        _btnClose.FlatAppearance.BorderSize = 0;
        _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 60, 60);
        _btnClose.Click += BtnClose_Click;

        _progressBar = new ProgressBar
        {
            Location = new Point(10, 32),
            Size = new Size(295, 20),
            Style = ProgressBarStyle.Continuous,
            Visible = false
        };

        _lblProgress = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 8),
            ForeColor = Color.White,
            Location = new Point(10, 54),
            Size = new Size(295, 15),
            Visible = false
        };

        Controls.AddRange(new Control[] { _lblMessage, _btnUpdate, _btnClose, _progressBar, _lblProgress });

        ResumeLayout(false);
    }

    public void ShowUpdate(UpdateCheckResult updateInfo)
    {
        _updateInfo = updateInfo;
        _lblMessage.Text = $"🔔 Yeni sürüm: {updateInfo.NewVersion} (Mevcut: {updateInfo.CurrentVersion})";
        
        _btnUpdate.Visible = true;
        _progressBar.Visible = false;
        _lblProgress.Visible = false;
        _btnClose.Visible = true;
        
        Visible = true;
        BringToFront();
    }

    public new void Hide()
    {
        Visible = false;
    }

    private async void BtnUpdate_Click(object? sender, EventArgs e)
    {
        if (_isUpdating || _updateInfo == null) return;
        _isUpdating = true;

        _btnUpdate.Visible = false;
        _btnClose.Visible = false;
        _progressBar.Visible = true;
        _lblProgress.Visible = true;
        _progressBar.Value = 0;
        _lblProgress.Text = "İndirme başlatılıyor...";
        
        Height = 75;
        BackColor = Color.FromArgb(60, 60, 65);

        UpdateStarted?.Invoke(this, EventArgs.Empty);

        var progress = new Progress<int>(percent =>
        {
            _progressBar.Value = percent;
            _lblProgress.Text = $"İndiriliyor... %{percent}";
        });

        try
        {
            var success = await _updateService.DownloadAndInstallUpdateAsync(
                _updateInfo.DownloadUrl!, 
                _updateInfo.NewVersion!, 
                progress);

            if (success)
            {
                _lblProgress.Text = "Güncelleme hazırlanıyor, uygulama kapanacak...";
                _lblMessage.Text = "✓ Güncelleme başarılı!";
                BackColor = Color.FromArgb(50, 150, 80);
                
                await Task.Delay(1500);
                Environment.Exit(0);
            }
            else
            {
                var errorMsg = _updateService.LastError ?? "Bilinmeyen hata";
                ShowError($"İndirilemedi: {errorMsg}");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Hata: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        _isUpdating = false;
        _lblProgress.Text = message;
        _lblMessage.Text = "❌ Güncelleme başarısız";
        BackColor = Color.FromArgb(180, 60, 60);
        _btnClose.Visible = true;
        
        // 3 saniye sonra tekrar dene butonu göster
        Task.Delay(3000).ContinueWith(_ =>
        {
            if (IsDisposed) return;
            Invoke(() =>
            {
                _progressBar.Visible = false;
                _lblProgress.Visible = false;
                _btnUpdate.Text = "Tekrar Dene";
                _btnUpdate.Visible = true;
                Height = 70;
                BackColor = Color.FromArgb(0, 100, 180);
                _lblMessage.Text = $"🔔 Yeni sürüm: {_updateInfo?.NewVersion}";
            });
        });
    }

    private void BtnClose_Click(object? sender, EventArgs e)
    {
        Visible = false;
        PanelClosed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdatePosition(int parentWidth)
    {
        Location = new Point(parentWidth - Width - 15, 10);
    }
}
