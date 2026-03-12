using System.Drawing.Drawing2D;
using KosmosAdapterV2.Application.UseCases;
using KosmosAdapterV2.Configuration;
using KosmosAdapterV2.Core.Enums;
using KosmosAdapterV2.Core.Models;
using KosmosAdapterV2.Infrastructure.Services;
using KosmosAdapterV2.UI.Controls;
using Microsoft.Extensions.Logging;
using WinFormsApp = System.Windows.Forms.Application;

namespace KosmosAdapterV2.UI.Forms;

public partial class ImageEditorForm : Form, IMessageFilter
{
    private readonly IScanImageUseCase _scanUseCase;
    private readonly IProcessImageUseCase _processImageUseCase;
    private readonly ILogger<ImageEditorForm> _logger;
    private readonly AppSettings _appSettings;

    private KosmosRequest? _request;
    private Bitmap? _originalImage;
    private Bitmap? _croppedImage;
    private Bitmap? _displayImage;
    private Graphics? _displayGraphics;
    
    private float _rotationAngle = 0f;
    private bool _isDrawing;
    private bool _isDrawMode;
    private bool _isColorPickMode;
    private bool _isMessageFilterActive;
    private Point _startPoint;
    private Point _endPoint;
    private Color _drawColor = Color.White;

    private PictureBox _pictureBox = null!;
    private Panel _toolPanel = null!;
    private Panel _imagePanel = null!;
    private Button _btnScan = null!;
    private Button _btnCrop = null!;
    private Button _btnDraw = null!;
    private Button _btnColor = null!;
    private Button _btnSave = null!;
    private Button _btnClear = null!;
    private Button _btnUndo = null!;
    private Button _btnRedo = null!;
    private Button _btnZoomIn = null!;
    private Button _btnZoomOut = null!;
    private Button _btnZoomFit = null!;
    private Label _lblZoom = null!;
    private NumericUpDown _numAngle = null!;
    private PictureBox _colorPreview = null!;
    private PictureBox _colorPicker = null!;
    private float _zoomLevel = 1.0f;
    private const float ZoomStep = 0.1f;
    private const float MinZoom = 0.1f;
    private const float MaxZoom = 5.0f;
    
    private readonly Stack<Bitmap> _undoStack = new();
    private readonly Stack<Bitmap> _redoStack = new();
    private const int MaxHistorySize = 20;

    private UpdateNotificationPanel? _updatePanel;
    private AutoUpdateService? _autoUpdateService;
    private Label _lblVersionInfo = null!;

    public ImageEditorForm(
        IScanImageUseCase scanUseCase,
        IProcessImageUseCase processImageUseCase,
        ILogger<ImageEditorForm> logger,
        AppSettings appSettings)
    {
        _scanUseCase = scanUseCase;
        _processImageUseCase = processImageUseCase;
        _logger = logger;
        _appSettings = appSettings;

        InitializeComponent();
        SetupEventHandlers();
        InitializeUpdatePanel();
    }

    /// <summary>
    /// Geçerli URL ile API yükleme açılır; null veya eksik cid/secret ile sadece tarama/dosya aç (okuma/yükleme yok).
    /// </summary>
    public void SetRequest(KosmosRequest? request, string? rawUrl = null)
    {
        _request = request?.IsValidForApi == true ? request : null;
        if (_request == null && !string.IsNullOrWhiteSpace(rawUrl))
            _logger.LogWarning("Tarayıcı açıldı; URL API için geçersiz/eksik, kaydetme devre dışı. Url={Url}", KosmosRequest.SanitizeUrlForLogging(rawUrl));
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        // Tool Panel
        _toolPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 180,
            BackColor = Color.FromArgb(45, 45, 48)
        };

        // Logo
        var logoBox = new PictureBox
        {
            Location = new Point(10, 8),
            Size = new Size(120, 50),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try
        {
            using var stream = typeof(ImageEditorForm).Assembly.GetManifestResourceStream("KosmosAdapterV2.Resources.logo.png");
            if (stream != null)
                logoBox.Image = Image.FromStream(stream);
        }
        catch { }

        // Scan Button
        _btnScan = CreateButtonWithIcon("🖨️", "Tara", 65, Color.FromArgb(0, 122, 204));
        _btnScan.Click += BtnScan_Click;

        // Open File Button
        var btnOpen = CreateButtonWithIcon("📂", "Dosya Aç", 105, Color.FromArgb(0, 122, 204));
        btnOpen.Click += BtnOpen_Click;

        // Separator
        var sep1 = CreateSeparator(150);

        // Crop Button
        _btnCrop = CreateButtonWithIcon("✂️", "Kırp", 160, Color.FromArgb(104, 104, 104));
        _btnCrop.Click += BtnCrop_Click;

        // Draw Button
        _btnDraw = CreateButtonWithIcon("✏️", "Çizim", 200, Color.FromArgb(104, 104, 104));
        _btnDraw.Click += BtnDraw_Click;

        // Color Button
        _btnColor = CreateButtonWithIcon("🎨", "Renk Seç", 240, Color.FromArgb(104, 104, 104));
        _btnColor.Click += BtnColor_Click;

        // Color Preview
        var lblColorPreview = new Label
        {
            Text = "Renk:",
            Location = new Point(10, 285),
            Size = new Size(40, 15),
            ForeColor = Color.White
        };

        _colorPicker = new PictureBox
        {
            Location = new Point(55, 282),
            Size = new Size(35, 25),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        _colorPreview = new PictureBox
        {
            Location = new Point(95, 282),
            Size = new Size(35, 25),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Separator
        var sep2 = CreateSeparator(315);

        // Angle Label
        var lblAngle = new Label
        {
            Text = "🔄 Döndür:",
            Location = new Point(10, 328),
            Size = new Size(70, 15),
            ForeColor = Color.White
        };

        // Angle NumericUpDown
        _numAngle = new NumericUpDown
        {
            Location = new Point(80, 325),
            Size = new Size(50, 23),
            Minimum = -360,
            Maximum = 360,
            Value = 0
        };
        _numAngle.ValueChanged += NumAngle_ValueChanged;

        // Zoom Label
        var lblZoomTitle = new Label
        {
            Text = "🔍 Yakınlaştır:",
            Location = new Point(10, 360),
            Size = new Size(85, 15),
            ForeColor = Color.White
        };

        // Zoom Controls
        _btnZoomOut = CreateButton("-", 380, Color.FromArgb(70, 70, 70));
        _btnZoomOut.Location = new Point(10, 380);
        _btnZoomOut.Size = new Size(35, 28);
        _btnZoomOut.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        _btnZoomOut.Click += BtnZoomOut_Click;

        _lblZoom = new Label
        {
            Text = "100%",
            Location = new Point(48, 385),
            Size = new Size(45, 18),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _btnZoomIn = CreateButton("+", 380, Color.FromArgb(70, 70, 70));
        _btnZoomIn.Location = new Point(95, 380);
        _btnZoomIn.Size = new Size(35, 28);
        _btnZoomIn.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        _btnZoomIn.Click += BtnZoomIn_Click;

        _btnZoomFit = CreateButtonWithIcon("⬜", "Sığdır", 415, Color.FromArgb(70, 70, 70));
        _btnZoomFit.Click += BtnZoomFit_Click;

        // Separator
        var sep3 = CreateSeparator(460);

        // Undo/Redo Buttons
        _btnUndo = CreateButton("↩️ Geri Al", 470, Color.FromArgb(80, 80, 80));
        _btnUndo.Location = new Point(10, 470);
        _btnUndo.Size = new Size(58, 32);
        _btnUndo.Font = new Font("Segoe UI", 8);
        _btnUndo.Click += BtnUndo_Click;
        _btnUndo.Enabled = false;

        _btnRedo = CreateButton("İleri ↪️", 470, Color.FromArgb(80, 80, 80));
        _btnRedo.Location = new Point(72, 470);
        _btnRedo.Size = new Size(58, 32);
        _btnRedo.Font = new Font("Segoe UI", 8);
        _btnRedo.Click += BtnRedo_Click;
        _btnRedo.Enabled = false;

        // Separator
        var sep4 = CreateSeparator(510);

        // Save Button
        _btnSave = CreateButtonWithIcon("💾", "Kaydet", 520, Color.FromArgb(0, 122, 204));
        _btnSave.Click += BtnSave_Click;

        // Clear Button
        _btnClear = CreateButtonWithIcon("🗑️", "Temizle", 560, Color.FromArgb(180, 60, 60));
        _btnClear.Click += BtnClear_Click;

        // Bottom Panel with Logo and Company Name (anchored to bottom)
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            BackColor = Color.FromArgb(35, 35, 38)
        };

        var bottomLogo = new PictureBox
        {
            Location = new Point(8, 5),
            Size = new Size(30, 30),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try
        {
            using var stream2 = typeof(ImageEditorForm).Assembly.GetManifestResourceStream("KosmosAdapterV2.Resources.logo.png");
            if (stream2 != null)
                bottomLogo.Image = Image.FromStream(stream2);
        }
        catch { }

        var lblCompany = new Label
        {
            Text = "Sanalogi",
            Location = new Point(42, 10),
            Size = new Size(90, 20),
            ForeColor = Color.FromArgb(150, 150, 150),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        bottomPanel.Controls.Add(bottomLogo);
        bottomPanel.Controls.Add(lblCompany);

        _toolPanel.Controls.AddRange(new Control[]
        {
            logoBox, _btnScan, btnOpen, sep1,
            _btnCrop, _btnDraw, _btnColor, lblColorPreview, _colorPicker, _colorPreview, sep2,
            lblAngle, _numAngle, lblZoomTitle, _btnZoomOut, _lblZoom, _btnZoomIn, _btnZoomFit, sep3,
            _btnUndo, _btnRedo, sep4, _btnSave, _btnClear, bottomPanel
        });

        // Image Panel (scrollable container)
        _imagePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            AutoScroll = true
        };

        // Picture Box
        _pictureBox = new PictureBox
        {
            Location = new Point(0, 0),
            BackColor = Color.Black,
            Cursor = Cursors.Cross,
            SizeMode = PictureBoxSizeMode.AutoSize
        };
        _pictureBox.MouseDown += PictureBox_MouseDown;
        _pictureBox.MouseMove += PictureBox_MouseMove;
        _pictureBox.MouseUp += PictureBox_MouseUp;

        _imagePanel.Controls.Add(_pictureBox);
        _imagePanel.MouseWheel += PictureBox_MouseWheel;

        // Footer Panel with version info
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 25,
            BackColor = Color.FromArgb(30, 30, 32)
        };

        _lblVersionInfo = new Label
        {
            Text = GetVersionInfoText(),
            Font = new Font("Segoe UI", 8),
            ForeColor = Color.FromArgb(180, 180, 180),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 10, 0)
        };

        footerPanel.Controls.Add(_lblVersionInfo);

        // Form settings
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1118, 705);
        Controls.Add(_imagePanel);
        Controls.Add(footerPanel);
        Controls.Add(_toolPanel);
        MinimumSize = new Size(900, 725);
        Name = "ImageEditorForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Fotoğraf Düzenle - Kosmos Adapter V2";
        
        try
        {
            using var stream = typeof(ImageEditorForm).Assembly.GetManifestResourceStream("KosmosAdapterV2.Resources.logo.png");
            if (stream != null)
            {
                using var bmp = new Bitmap(stream);
                Icon = Icon.FromHandle(bmp.GetHicon());
            }
        }
        catch { }

        Load += ImageEditorForm_Load;
        FormClosing += ImageEditorForm_FormClosing;
        Resize += ImageEditorForm_Resize;

        ResumeLayout(false);
    }

    private void ImageEditorForm_Resize(object? sender, EventArgs e)
    {
        CenterImageInPanel();
        _updatePanel?.UpdatePosition(_imagePanel.Width);
    }

    private Button CreateButton(string text, int top, Color backColor)
    {
        return new Button
        {
            Text = text,
            Location = new Point(10, top),
            Size = new Size(120, 32),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
    }

    private Button CreateButtonWithIcon(string icon, string text, int top, Color backColor)
    {
        return new Button
        {
            Text = $"{icon}  {text}",
            Location = new Point(10, top),
            Size = new Size(120, 35),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0),
            Font = new Font("Segoe UI", 9)
        };
    }

    private Panel CreateSeparator(int top)
    {
        return new Panel
        {
            Location = new Point(10, top),
            Size = new Size(120, 1),
            BackColor = Color.FromArgb(80, 80, 80)
        };
    }

    private void SetupEventHandlers()
    {
        _scanUseCase.ImageScanned += OnImageScanned;
    }

    private void InitializeUpdatePanel()
    {
        _autoUpdateService = new AutoUpdateService(_appSettings);
        _updatePanel = new UpdateNotificationPanel(_autoUpdateService);
        _updatePanel.PanelClosed += (s, e) => _logger.LogInformation("Update notification dismissed");
        _imagePanel.Controls.Add(_updatePanel);
        _updatePanel.BringToFront();
    }

    private string GetVersionInfoText()
    {
        var version = _autoUpdateService?.GetLocalVersion() ?? _appSettings.Version;
        var updateDate = GetLastUpdateDate();
        
        if (!string.IsNullOrEmpty(updateDate))
            return $"v{version}  •  Son güncelleme: {updateDate}";
        
        return $"v{version}";
    }

    private string? GetLastUpdateDate()
    {
        try
        {
            var versionFile = Path.Combine(AppContext.BaseDirectory, "version.local.json");
            if (File.Exists(versionFile))
            {
                var json = File.ReadAllText(versionFile);
                var info = System.Text.Json.JsonSerializer.Deserialize<Infrastructure.Services.LocalVersionInfo>(json, 
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (info?.UpdatedAt != null && DateTime.TryParse(info.UpdatedAt, out var date))
                {
                    return date.ToString("dd.MM.yyyy HH:mm");
                }
            }
        }
        catch { }
        return null;
    }

    private async void CheckForUpdatesAsync()
    {
        if (_autoUpdateService == null || _updatePanel == null || !_appSettings.EnableAutoUpdate)
            return;

        try
        {
            var result = await _autoUpdateService.CheckForUpdateAsync();

            if (result.HasUpdate && !string.IsNullOrEmpty(result.NewVersion))
            {
                _logger.LogInformation("Update available: {NewVersion} (Current: {CurrentVersion})", 
                    result.NewVersion, result.CurrentVersion);
                
                Invoke(() =>
                {
                    _updatePanel.UpdatePosition(_imagePanel.Width);
                    _updatePanel.ShowUpdate(result);
                });
            }
            else if (!string.IsNullOrEmpty(result.Error))
            {
                _logger.LogWarning("Update check failed: {Error}", result.Error);
            }
            else
            {
                _logger.LogInformation("Application is up to date. Version: {Version}", result.CurrentVersion);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for updates");
        }
    }

    private void ImageEditorForm_Load(object? sender, EventArgs e)
    {
        ClearImages();
        _zoomLevel = 1.0f;
        _lblZoom.Text = "100%";
        _rotationAngle = 0f;
        _numAngle.Value = 0;
        
        _scanUseCase.CloseScan();
        _scanUseCase.Initialize(Handle);
        _logger.LogInformation("Image editor form loaded");

        // Güncelleme kontrolü (arka planda)
        Task.Run(() => CheckForUpdatesAsync());
    }

    private void ImageEditorForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_isMessageFilterActive)
        {
            WinFormsApp.RemoveMessageFilter(this);
            _isMessageFilterActive = false;
        }

        _scanUseCase.CloseScan();
        ClearHistory();
        
        _displayGraphics?.Dispose();
        _displayImage?.Dispose();
        _croppedImage?.Dispose();
        _originalImage?.Dispose();

        _logger.LogInformation("Image editor form closing");
    }

    private void BtnScan_Click(object? sender, EventArgs e)
    {
        try
        {
            ClearImages();

            if (!_isMessageFilterActive)
            {
                Enabled = false;
                _isMessageFilterActive = true;
                WinFormsApp.AddMessageFilter(this);
            }

            _scanUseCase.CloseScan();
            
            if (!_scanUseCase.StartScan())
            {
                EndScan();
                MessageBox.Show("Tarayıcı başlatılamadı!\n\nLütfen:\n1. Tarayıcınızın bağlı olduğundan emin olun\n2. TWAIN driver'ın yüklü olduğunu kontrol edin\n\nAlternatif olarak 'Dosya Aç' butonunu kullanabilirsiniz.", 
                    "Tarayıcı Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            EndScan();
            _logger.LogError(ex, "Error starting scan");
            MessageBox.Show($"Tarama hatası: {ex.Message}\n\n'Dosya Aç' butonunu kullanarak resim yükleyebilirsiniz.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnOpen_Click(object? sender, EventArgs e)
    {
        try
        {
            using var openDialog = new OpenFileDialog
            {
                Title = "Resim Seç",
                Filter = "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff|Tüm Dosyalar|*.*",
                FilterIndex = 1
            };

            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                ClearImages();
                
                using var fileStream = new FileStream(openDialog.FileName, FileMode.Open, FileAccess.Read);
                var loadedImage = Image.FromStream(fileStream);
                SetImage(new Bitmap(loadedImage));
                
                _logger.LogInformation("Image loaded from file: {FileName}", openDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening file");
            MessageBox.Show($"Dosya açma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnCrop_Click(object? sender, EventArgs e)
    {
        SetMode(crop: true);
        _btnCrop.BackColor = Color.FromArgb(0, 122, 204);
    }

    private void BtnDraw_Click(object? sender, EventArgs e)
    {
        SetMode(draw: true);
        _btnDraw.BackColor = Color.FromArgb(0, 122, 204);
        _pictureBox.Cursor = Cursors.Hand;
    }

    private void BtnColor_Click(object? sender, EventArgs e)
    {
        SetMode(colorPick: true);
        _btnColor.BackColor = Color.FromArgb(0, 122, 204);
        _pictureBox.Cursor = Cursors.Cross;
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_pictureBox.Image == null)
        {
            MessageBox.Show("Kaydedilecek görüntü yok!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_request == null)
        {
            MessageBox.Show(
                "Bu oturumda API'ye okuma/yükleme yapılamaz.\n\nGeçerli bir kosmos2:// linki gerekir (cid ve secret ile).\nSadece tarama veya dosya açabilirsiniz.",
                "URL geçersiz veya eksik",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            
            var result = await _processImageUseCase.UploadImageAsync(_request, _pictureBox.Image);
            
            Cursor = Cursors.Default;

            if (result.Success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show($"Kaydetme hatası: {result.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            _logger.LogError(ex, "Error saving image");
            MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void NumAngle_ValueChanged(object? sender, EventArgs e)
    {
        if (_originalImage == null) return;

        try
        {
            SaveToHistory();
            _rotationAngle = (float)_numAngle.Value;
            _croppedImage = _processImageUseCase.Rotate(_originalImage, _rotationAngle);
            UpdateDisplayImage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating image");
        }
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Mevcut taramayı temizlemek istediğinizden emin misiniz?",
            "Temizle",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            ClearImages();
            ClearHistory();
            _zoomLevel = 1.0f;
            _lblZoom.Text = "100%";
            _rotationAngle = 0f;
            _numAngle.Value = 0;
            _logger.LogInformation("Image cleared by user");
        }
    }

    private void BtnUndo_Click(object? sender, EventArgs e)
    {
        if (_undoStack.Count == 0 || _croppedImage == null) return;

        _redoStack.Push(new Bitmap(_croppedImage));
        
        _croppedImage.Dispose();
        _croppedImage = _undoStack.Pop();
        
        UpdateDisplayImage();
        UpdateUndoRedoButtons();
        _logger.LogInformation("Undo performed, {UndoCount} undo steps remaining", _undoStack.Count);
    }

    private void BtnRedo_Click(object? sender, EventArgs e)
    {
        if (_redoStack.Count == 0) return;

        if (_croppedImage != null)
        {
            _undoStack.Push(new Bitmap(_croppedImage));
            _croppedImage.Dispose();
        }
        
        _croppedImage = _redoStack.Pop();
        
        UpdateDisplayImage();
        UpdateUndoRedoButtons();
        _logger.LogInformation("Redo performed, {RedoCount} redo steps remaining", _redoStack.Count);
    }

    private void SaveToHistory()
    {
        if (_croppedImage == null) return;

        if (_undoStack.Count >= MaxHistorySize)
        {
            var oldestItems = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < oldestItems.Length - 1; i++)
            {
                if (i == oldestItems.Length - 1)
                    oldestItems[i].Dispose();
                else
                    _undoStack.Push(oldestItems[i]);
            }
        }

        _undoStack.Push(new Bitmap(_croppedImage));
        
        foreach (var bitmap in _redoStack)
            bitmap.Dispose();
        _redoStack.Clear();
        
        UpdateUndoRedoButtons();
    }

    private void UpdateUndoRedoButtons()
    {
        _btnUndo.Enabled = _undoStack.Count > 0;
        _btnRedo.Enabled = _redoStack.Count > 0;
    }

    private void ClearHistory()
    {
        foreach (var bitmap in _undoStack)
            bitmap.Dispose();
        _undoStack.Clear();
        
        foreach (var bitmap in _redoStack)
            bitmap.Dispose();
        _redoStack.Clear();
        
        UpdateUndoRedoButtons();
    }

    private void BtnZoomIn_Click(object? sender, EventArgs e)
    {
        SetZoom(_zoomLevel + ZoomStep);
    }

    private void BtnZoomOut_Click(object? sender, EventArgs e)
    {
        SetZoom(_zoomLevel - ZoomStep);
    }

    private void BtnZoomFit_Click(object? sender, EventArgs e)
    {
        FitToWindow();
    }

    private void PictureBox_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (ModifierKeys == Keys.Control)
        {
            var delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
            SetZoom(_zoomLevel + delta);
        }
    }

    private void SetZoom(float newZoom)
    {
        _zoomLevel = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));
        _lblZoom.Text = $"{(int)(_zoomLevel * 100)}%";
        UpdateDisplayImage();
    }

    private void FitToWindow()
    {
        if (_croppedImage == null) return;

        var widthRatio = (float)_imagePanel.ClientSize.Width / _croppedImage.Width;
        var heightRatio = (float)_imagePanel.ClientSize.Height / _croppedImage.Height;
        
        SetZoom(Math.Min(widthRatio, heightRatio));
    }

    private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_pictureBox.Image == null) return;

        try
        {
            if (_isColorPickMode)
            {
                PickColor(e.Location);
                return;
            }

            if (_isDrawMode && e.Button == MouseButtons.Left)
            {
                DrawPoint(e.Location);
                return;
            }

            UpdateDisplayImage();
            _isDrawing = true;
            _startPoint = e.Location;
            DrawSelectionBox(_startPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error on mouse down");
        }
    }

    private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_pictureBox.Image == null) return;

        try
        {
            if (_isColorPickMode)
            {
                PreviewColor(e.Location);
                return;
            }

            if (_isDrawMode && e.Button == MouseButtons.Left)
            {
                DrawPoint(e.Location);
                return;
            }

            if (_isDrawing)
            {
                DrawSelectionBox(e.Location);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error on mouse move");
        }
    }

    private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isDrawing || _croppedImage == null) return;
        _isDrawing = false;

        try
        {
            var displayX = Math.Min(_startPoint.X, _endPoint.X);
            var displayY = Math.Min(_startPoint.Y, _endPoint.Y);
            var displayWidth = Math.Abs(_startPoint.X - _endPoint.X);
            var displayHeight = Math.Abs(_startPoint.Y - _endPoint.Y);

            if (displayWidth == 0 || displayHeight == 0) return;

            var x = (int)(displayX / _zoomLevel);
            var y = (int)(displayY / _zoomLevel);
            var height = (int)(displayHeight / _zoomLevel);
            var aspectWidth = height * 3 / 4;

            x = Math.Max(0, Math.Min(x, _croppedImage.Width - 1));
            y = Math.Max(0, Math.Min(y, _croppedImage.Height - 1));
            aspectWidth = Math.Min(aspectWidth, _croppedImage.Width - x);
            height = Math.Min(height, _croppedImage.Height - y);

            if (aspectWidth <= 0 || height <= 0) return;

            SaveToHistory();
            
            var region = new CropRegion(x, y, aspectWidth, height);

            _croppedImage = _processImageUseCase.Crop(_croppedImage, region);
            UpdateDisplayImage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error on mouse up");
        }
    }

    public bool PreFilterMessage(ref Message m)
    {
        var cmd = _scanUseCase.ProcessMessage(ref m);

        if (cmd == TwainCommand.Not)
            return false;

        switch (cmd)
        {
            case TwainCommand.CloseRequest:
            case TwainCommand.CloseOk:
                EndScan();
                _scanUseCase.CloseScan();
                break;

            case TwainCommand.TransferReady:
                try
                {
                    var images = _scanUseCase.GetScannedImages().ToList();
                    EndScan();
                    _scanUseCase.CloseScan();

                    if (images.Count > 0)
                    {
                        SetImage(images[0]);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error transferring images");
                    MessageBox.Show($"Görüntü aktarım hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                break;
        }

        return true;
    }

    private void OnImageScanned(object? sender, Bitmap bitmap)
    {
        Invoke(() => SetImage(bitmap));
    }

    private void SetImage(Bitmap image)
    {
        ClearImages();
        
        _originalImage = new Bitmap(image);
        _croppedImage = new Bitmap(image);
        
        FitToWindow();
    }

    private void UpdateDisplayImage()
    {
        if (_croppedImage == null) return;

        var width = (int)(_zoomLevel * _croppedImage.Width);
        var height = (int)(_zoomLevel * _croppedImage.Height);

        if (width <= 0 || height <= 0) return;

        var scaledImage = new Bitmap(width, height);
        using (var gr = Graphics.FromImage(scaledImage))
        {
            gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
            gr.InterpolationMode = InterpolationMode.HighQualityBilinear;
            gr.DrawImage(_croppedImage, new Rectangle(0, 0, width, height),
                new Rectangle(0, 0, _croppedImage.Width, _croppedImage.Height), GraphicsUnit.Pixel);
        }

        _pictureBox.Image = null;
        
        _displayGraphics?.Dispose();
        _displayImage?.Dispose();
        
        _displayImage = scaledImage;
        _displayGraphics = Graphics.FromImage(_displayImage);

        _pictureBox.SizeMode = PictureBoxSizeMode.Normal;
        _pictureBox.Size = new Size(width, height);
        _pictureBox.Image = _displayImage;
        
        CenterImageInPanel();
    }

    private void CenterImageInPanel()
    {
        if (_pictureBox.Image == null) return;

        var x = Math.Max(0, (_imagePanel.ClientSize.Width - _pictureBox.Width) / 2);
        var y = Math.Max(0, (_imagePanel.ClientSize.Height - _pictureBox.Height) / 2);
        
        _pictureBox.Location = new Point(x, y);
    }

    private void DrawSelectionBox(Point endPoint)
    {
        if (_displayImage == null || _displayGraphics == null) return;

        _endPoint = endPoint;
        _endPoint.X = Math.Max(0, Math.Min(_endPoint.X, _displayImage.Width - 1));
        _endPoint.Y = Math.Max(0, Math.Min(_endPoint.Y, _displayImage.Height - 1));

        var displayHeight = Math.Abs(_startPoint.Y - _endPoint.Y);
        
        var realHeight = (int)(displayHeight / _zoomLevel);
        var realAspectWidth = realHeight * 3 / 4;
        var displayAspectWidth = (int)(realAspectWidth * _zoomLevel);

        var x = Math.Min(_startPoint.X, _endPoint.X);
        var y = Math.Min(_startPoint.Y, _endPoint.Y);

        UpdateDisplayImage();
        using var pen = new Pen(Color.Red, 2);
        _displayGraphics?.DrawRectangle(pen, x, y, displayAspectWidth, displayHeight);
        _pictureBox.Refresh();
    }

    private void DrawPoint(Point location)
    {
        if (_displayImage == null || _displayGraphics == null) return;

        using var pen = new Pen(_drawColor, 4);
        _displayGraphics.DrawRectangle(pen, location.X, location.Y, 2, 2);
        _pictureBox.Refresh();
    }

    private void PickColor(Point location)
    {
        if (_displayImage == null) return;

        try
        {
            _drawColor = _displayImage.GetPixel(location.X, location.Y);
            _colorPreview.BackColor = _drawColor;
        }
        catch { }
    }

    private void PreviewColor(Point location)
    {
        if (_displayImage == null) return;

        try
        {
            var color = _displayImage.GetPixel(location.X, location.Y);
            _colorPicker.BackColor = color;
        }
        catch { }
    }

    private void SetMode(bool crop = false, bool draw = false, bool colorPick = false)
    {
        _isDrawMode = draw;
        _isColorPickMode = colorPick;

        _btnCrop.BackColor = Color.FromArgb(104, 104, 104);
        _btnDraw.BackColor = Color.FromArgb(104, 104, 104);
        _btnColor.BackColor = Color.FromArgb(104, 104, 104);

        _pictureBox.Cursor = Cursors.Cross;
    }

    private void ClearImages()
    {
        _pictureBox.Image = null;
        
        _displayGraphics?.Dispose();
        _displayImage?.Dispose();
        _croppedImage?.Dispose();
        _originalImage?.Dispose();

        _displayGraphics = null;
        _displayImage = null;
        _croppedImage = null;
        _originalImage = null;
        
        _pictureBox.Size = new Size(1, 1);

        GC.Collect();
    }

    private void EndScan()
    {
        if (_isMessageFilterActive)
        {
            WinFormsApp.RemoveMessageFilter(this);
            _isMessageFilterActive = false;
            Enabled = true;
            Activate();
        }
    }
}
