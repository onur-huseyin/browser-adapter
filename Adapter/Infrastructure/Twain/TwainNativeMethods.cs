using System.Runtime.InteropServices;

namespace KosmosAdapterV2.Infrastructure.Twain;

internal static class TwainNativeMethods
{
    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DSMparent(
        [In, Out] TwIdentity origin,
        IntPtr zeroptr,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        ref IntPtr refptr);

    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DSMident(
        [In, Out] TwIdentity origin,
        IntPtr zeroptr,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        [In, Out] TwIdentity idds);

    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DSMstatus(
        [In, Out] TwIdentity origin,
        IntPtr zeroptr,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        [In, Out] TwStatus dsmstat);

    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DSuserif(
        [In, Out] TwIdentity origin,
        [In, Out] TwIdentity dest,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        TwUserInterface guif);

    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DSevent(
        [In, Out] TwIdentity origin,
        [In, Out] TwIdentity dest,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        ref TwEvent evt);

    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DSstatus(
        [In, Out] TwIdentity origin,
        [In] TwIdentity dest,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        [In, Out] TwStatus dsmstat);

    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DScap(
        [In, Out] TwIdentity origin,
        [In] TwIdentity dest,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        [In, Out] TwCapability capa);

    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DSiinf(
        [In, Out] TwIdentity origin,
        [In] TwIdentity dest,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        [In, Out] TwImageInfo imginf);

    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DSixfer(
        [In, Out] TwIdentity origin,
        [In] TwIdentity dest,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        ref IntPtr hbitmap);

    [DllImport("twain_32.dll", EntryPoint = "#1")]
    internal static extern TwRC DSpxfer(
        [In, Out] TwIdentity origin,
        [In] TwIdentity dest,
        TwDG dg,
        TwDAT dat,
        TwMSG msg,
        [In, Out] TwPendingXfers pxfr);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    internal static extern IntPtr GlobalAlloc(int flags, int size);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    internal static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    internal static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    internal static extern IntPtr GlobalFree(IntPtr handle);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int GetMessagePos();

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int GetMessageTime();

    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern int GetDeviceCaps(IntPtr hDC, int nIndex);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr CreateDC(string szdriver, string? szdevice, string? szoutput, IntPtr devmode);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern int SetDIBitsToDevice(
        IntPtr hdc,
        int xdst, int ydst,
        int width, int height,
        int xsrc, int ysrc,
        int start, int lines,
        IntPtr bitsptr,
        IntPtr bmiptr,
        int color);
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal class TwCapability : IDisposable
{
    public short Cap;
    public short ConType;
    public IntPtr Handle;
    private bool _disposed;

    public TwCapability(TwCap cap)
    {
        Cap = (short)cap;
        ConType = -1;
        Handle = IntPtr.Zero;
    }

    public TwCapability(TwCap cap, short sval)
    {
        Cap = (short)cap;
        ConType = (short)TwOn.One;
        Handle = TwainNativeMethods.GlobalAlloc(0x42, 6);
        var pv = TwainNativeMethods.GlobalLock(Handle);
        Marshal.WriteInt16(pv, 0, (short)TwType.Int16);
        Marshal.WriteInt32(pv, 2, sval);
        TwainNativeMethods.GlobalUnlock(Handle);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (Handle != IntPtr.Zero)
        {
            TwainNativeMethods.GlobalFree(Handle);
            Handle = IntPtr.Zero;
        }
        
        _disposed = true;
    }

    ~TwCapability()
    {
        Dispose(false);
    }
}
