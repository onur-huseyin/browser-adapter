using System.Runtime.InteropServices;
using KosmosAdapterV2.Core.Enums;
using KosmosAdapterV2.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace KosmosAdapterV2.Infrastructure.Twain;

public sealed class TwainScannerService : IScannerService
{
    private const short CountryUSA = 1;
    private const short LanguageUSA = 13;

    private readonly ILogger<TwainScannerService> _logger;
    private readonly TwIdentity _appId;
    private readonly TwIdentity _sourceId;
    private TwEvent _eventMsg;
    private WinMsg _winMsg;
    private IntPtr _windowHandle;
    private bool _disposed;

    public event EventHandler<Bitmap>? ImageScanned;

    public TwainScannerService(ILogger<TwainScannerService> logger)
    {
        _logger = logger;
        
        _appId = new TwIdentity
        {
            Id = IntPtr.Zero,
            Version = new TwVersion
            {
                MajorNum = 2,
                MinorNum = 0,
                Language = LanguageUSA,
                Country = CountryUSA,
                Info = "KosmosAdapter V2"
            },
            ProtocolMajor = TwProtocol.Major,
            ProtocolMinor = TwProtocol.Minor,
            SupportedGroups = (int)(TwDG.Image | TwDG.Control),
            Manufacturer = "Sanalogi",
            ProductFamily = "Kosmos",
            ProductName = "KosmosAdapterV2"
        };

        _sourceId = new TwIdentity { Id = IntPtr.Zero };
        _eventMsg.EventPtr = Marshal.AllocHGlobal(Marshal.SizeOf(_winMsg));
    }

    public static int ScreenBitDepth
    {
        get
        {
            var screenDC = TwainNativeMethods.CreateDC("DISPLAY", null, null, IntPtr.Zero);
            var bitDepth = TwainNativeMethods.GetDeviceCaps(screenDC, 12);
            bitDepth *= TwainNativeMethods.GetDeviceCaps(screenDC, 14);
            TwainNativeMethods.DeleteDC(screenDC);
            return bitDepth;
        }
    }

    public bool Initialize(IntPtr windowHandle)
    {
        try
        {
            Finish();
            
            var rc = TwainNativeMethods.DSMparent(_appId, IntPtr.Zero, TwDG.Control, TwDAT.Parent, TwMSG.OpenDSM, ref windowHandle);
            
            if (rc == TwRC.Success)
            {
                rc = TwainNativeMethods.DSMident(_appId, IntPtr.Zero, TwDG.Control, TwDAT.Identity, TwMSG.GetDefault, _sourceId);
                if (rc == TwRC.Success)
                {
                    _windowHandle = windowHandle;
                    _logger.LogInformation("TWAIN initialized successfully");
                    return true;
                }
                
                TwainNativeMethods.DSMparent(_appId, IntPtr.Zero, TwDG.Control, TwDAT.Parent, TwMSG.CloseDSM, ref windowHandle);
            }

            _logger.LogWarning("Failed to initialize TWAIN: {ResultCode}", rc);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing TWAIN");
            return false;
        }
    }

    public bool SelectSource()
    {
        try
        {
            CloseScan();
            
            if (_appId.Id == IntPtr.Zero)
            {
                if (!Initialize(_windowHandle))
                    return false;
            }

            var rc = TwainNativeMethods.DSMident(_appId, IntPtr.Zero, TwDG.Control, TwDAT.Identity, TwMSG.UserSelect, _sourceId);
            
            _logger.LogInformation("Source selection: {ResultCode}", rc);
            return rc == TwRC.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting source");
            return false;
        }
    }

    public bool StartScan()
    {
        try
        {
            CloseScan();

            if (_appId.Id == IntPtr.Zero)
            {
                if (!Initialize(_windowHandle))
                    return false;
            }

            var rc = TwainNativeMethods.DSMident(_appId, IntPtr.Zero, TwDG.Control, TwDAT.Identity, TwMSG.OpenDS, _sourceId);
            if (rc != TwRC.Success)
            {
                _logger.LogWarning("Failed to open data source: {ResultCode}", rc);
                return false;
            }

            using var cap = new TwCapability(TwCap.XferCount, 1);
            rc = TwainNativeMethods.DScap(_appId, _sourceId, TwDG.Control, TwDAT.Capability, TwMSG.Set, cap);
            if (rc != TwRC.Success)
            {
                _logger.LogWarning("Failed to set capability: {ResultCode}", rc);
                CloseScan();
                return false;
            }

            var gui = new TwUserInterface
            {
                ShowUI = 1,
                ModalUI = 1,
                ParentHand = _windowHandle
            };

            rc = TwainNativeMethods.DSuserif(_appId, _sourceId, TwDG.Control, TwDAT.UserInterface, TwMSG.EnableDS, gui);
            if (rc != TwRC.Success)
            {
                _logger.LogWarning("Failed to enable data source UI: {ResultCode}", rc);
                CloseScan();
                return false;
            }

            _logger.LogInformation("Scan started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting scan");
            return false;
        }
    }

    public TwainCommand ProcessMessage(ref Message m)
    {
        if (_sourceId.Id == IntPtr.Zero)
            return TwainCommand.Not;

        var pos = TwainNativeMethods.GetMessagePos();

        _winMsg.hwnd = m.HWnd;
        _winMsg.message = m.Msg;
        _winMsg.wParam = m.WParam;
        _winMsg.lParam = m.LParam;
        _winMsg.time = TwainNativeMethods.GetMessageTime();
        _winMsg.x = (short)pos;
        _winMsg.y = (short)(pos >> 16);

        Marshal.StructureToPtr(_winMsg, _eventMsg.EventPtr, false);
        _eventMsg.Message = 0;

        var rc = TwainNativeMethods.DSevent(_appId, _sourceId, TwDG.Control, TwDAT.Event, TwMSG.ProcessEvent, ref _eventMsg);

        if (rc == TwRC.NotDSEvent)
            return TwainCommand.Not;

        return _eventMsg.Message switch
        {
            (short)TwMSG.XFerReady => TwainCommand.TransferReady,
            (short)TwMSG.CloseDSReq => TwainCommand.CloseRequest,
            (short)TwMSG.CloseDSOK => TwainCommand.CloseOk,
            (short)TwMSG.DeviceEvent => TwainCommand.DeviceEvent,
            _ => TwainCommand.Null
        };
    }

    public IEnumerable<Bitmap> TransferImages()
    {
        var images = new List<Bitmap>();

        if (_sourceId.Id == IntPtr.Zero)
            return images;

        try
        {
            var pxfr = new TwPendingXfers();

            do
            {
                pxfr.Count = 0;
                var hbitmap = IntPtr.Zero;

                var iinf = new TwImageInfo();
                var rc = TwainNativeMethods.DSiinf(_appId, _sourceId, TwDG.Image, TwDAT.ImageInfo, TwMSG.Get, iinf);
                if (rc != TwRC.Success)
                {
                    _logger.LogWarning("Failed to get image info: {ResultCode}", rc);
                    CloseScan();
                    return images;
                }

                rc = TwainNativeMethods.DSixfer(_appId, _sourceId, TwDG.Image, TwDAT.ImageNativeXfer, TwMSG.Get, ref hbitmap);
                if (rc != TwRC.XferDone)
                {
                    _logger.LogWarning("Failed to transfer image: {ResultCode}", rc);
                    CloseScan();
                    return images;
                }

                rc = TwainNativeMethods.DSpxfer(_appId, _sourceId, TwDG.Control, TwDAT.PendingXfers, TwMSG.EndXfer, pxfr);
                if (rc != TwRC.Success)
                {
                    _logger.LogWarning("Failed to end transfer: {ResultCode}", rc);
                    CloseScan();
                    return images;
                }

                var bitmap = ConvertDibToBitmap(hbitmap);
                if (bitmap != null)
                {
                    images.Add(bitmap);
                    ImageScanned?.Invoke(this, bitmap);
                }
            }
            while (pxfr.Count != 0);

            TwainNativeMethods.DSpxfer(_appId, _sourceId, TwDG.Control, TwDAT.PendingXfers, TwMSG.Reset, pxfr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring images");
        }
        finally
        {
            CloseScan();
        }

        return images;
    }

    private Bitmap? ConvertDibToBitmap(IntPtr dibHandle)
    {
        try
        {
            var bitmapPointer = TwainNativeMethods.GlobalLock(dibHandle);
            var bitmapInfo = new BitmapInfoHeader();
            Marshal.PtrToStructure(bitmapPointer, bitmapInfo);

            if (bitmapInfo.biSizeImage == 0)
            {
                bitmapInfo.biSizeImage = ((((bitmapInfo.biWidth * bitmapInfo.biBitCount) + 31) & ~31) >> 3) * bitmapInfo.biHeight;
            }

            var pixelInfoPointer = bitmapInfo.biClrUsed;
            if (pixelInfoPointer == 0 && bitmapInfo.biBitCount <= 8)
            {
                pixelInfoPointer = 1 << bitmapInfo.biBitCount;
            }

            pixelInfoPointer = (pixelInfoPointer * 4) + bitmapInfo.biSize + (int)bitmapPointer;
            var pixelInfoIntPointer = new IntPtr(pixelInfoPointer);

            var bitmap = new Bitmap(bitmapInfo.biWidth, bitmapInfo.biHeight);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdc = graphics.GetHdc();
                try
                {
                    TwainNativeMethods.SetDIBitsToDevice(
                        hdc,
                        0, 0,
                        bitmapInfo.biWidth, bitmapInfo.biHeight,
                        0, 0,
                        0, bitmapInfo.biHeight,
                        pixelInfoIntPointer,
                        bitmapPointer,
                        0);
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            var dpiX = PpmToDpi(bitmapInfo.biXPelsPerMeter);
            var dpiY = PpmToDpi(bitmapInfo.biYPelsPerMeter);
            if (dpiX > 0 && dpiY > 0)
            {
                bitmap.SetResolution(dpiX, dpiY);
            }

            TwainNativeMethods.GlobalUnlock(dibHandle);
            TwainNativeMethods.GlobalFree(dibHandle);

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting DIB to bitmap");
            return null;
        }
    }

    private static float PpmToDpi(double pixelsPerMeter)
    {
        var pixelsPerMillimeter = pixelsPerMeter / 1000.0;
        var dotsPerInch = pixelsPerMillimeter * 25.4;
        return (float)Math.Round(dotsPerInch, 2);
    }

    public void CloseScan()
    {
        if (_sourceId.Id != IntPtr.Zero)
        {
            var gui = new TwUserInterface();
            TwainNativeMethods.DSuserif(_appId, _sourceId, TwDG.Control, TwDAT.UserInterface, TwMSG.DisableDS, gui);
            
            TwainNativeMethods.DSMident(_appId, IntPtr.Zero, TwDG.Control, TwDAT.Identity, TwMSG.CloseDS, _sourceId);
            _sourceId.Id = IntPtr.Zero;
        }
    }

    private void Finish()
    {
        CloseScan();
        
        if (_appId.Id != IntPtr.Zero)
        {
            TwainNativeMethods.DSMparent(_appId, IntPtr.Zero, TwDG.Control, TwDAT.Parent, TwMSG.CloseDSM, ref _windowHandle);
            _appId.Id = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Finish();

        if (_eventMsg.EventPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_eventMsg.EventPtr);
            _eventMsg.EventPtr = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
