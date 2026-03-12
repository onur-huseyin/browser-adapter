using System.Runtime.InteropServices;

namespace KosmosAdapterV2.Infrastructure.Twain;

internal static class TwProtocol
{
    public const short Major = 1;
    public const short Minor = 9;
}

[Flags]
internal enum TwDG : short
{
    Control = 0x0001,
    Image = 0x0002,
    Audio = 0x0004
}

internal enum TwDAT : short
{
    Null = 0x0000,
    Capability = 0x0001,
    Event = 0x0002,
    Identity = 0x0003,
    Parent = 0x0004,
    PendingXfers = 0x0005,
    SetupMemXfer = 0x0006,
    SetupFileXfer = 0x0007,
    Status = 0x0008,
    UserInterface = 0x0009,
    XferGroup = 0x000a,
    TwunkIdentity = 0x000b,
    CustomDSData = 0x000c,
    DeviceEvent = 0x000d,
    FileSystem = 0x000e,
    PassThru = 0x000f,
    ImageInfo = 0x0101,
    ImageLayout = 0x0102,
    ImageMemXfer = 0x0103,
    ImageNativeXfer = 0x0104,
    ImageFileXfer = 0x0105,
    CieColor = 0x0106,
    GrayResponse = 0x0107,
    RGBResponse = 0x0108,
    JpegCompression = 0x0109,
    Palette8 = 0x010a,
    ExtImageInfo = 0x010b,
    SetupFileXfer2 = 0x0301
}

internal enum TwMSG : short
{
    Null = 0x0000,
    Get = 0x0001,
    GetCurrent = 0x0002,
    GetDefault = 0x0003,
    GetFirst = 0x0004,
    GetNext = 0x0005,
    Set = 0x0006,
    Reset = 0x0007,
    QuerySupport = 0x0008,
    XFerReady = 0x0101,
    CloseDSReq = 0x0102,
    CloseDSOK = 0x0103,
    DeviceEvent = 0x0104,
    CheckStatus = 0x0201,
    OpenDSM = 0x0301,
    CloseDSM = 0x0302,
    OpenDS = 0x0401,
    CloseDS = 0x0402,
    UserSelect = 0x0403,
    DisableDS = 0x0501,
    EnableDS = 0x0502,
    EnableDSUIOnly = 0x0503,
    ProcessEvent = 0x0601,
    EndXfer = 0x0701,
    StopFeeder = 0x0702,
    ChangeDirectory = 0x0801,
    CreateDirectory = 0x0802,
    Delete = 0x0803,
    FormatMedia = 0x0804,
    GetClose = 0x0805,
    GetFirstFile = 0x0806,
    GetInfo = 0x0807,
    GetNextFile = 0x0808,
    Rename = 0x0809,
    Copy = 0x080A,
    AutoCaptureDir = 0x080B,
    PassThru = 0x0901
}

internal enum TwRC : short
{
    Success = 0x0000,
    Failure = 0x0001,
    CheckStatus = 0x0002,
    Cancel = 0x0003,
    DSEvent = 0x0004,
    NotDSEvent = 0x0005,
    XferDone = 0x0006,
    EndOfList = 0x0007,
    InfoNotSupported = 0x0008,
    DataNotAvailable = 0x0009
}

internal enum TwOn : short
{
    Array = 0x0003,
    Enum = 0x0004,
    One = 0x0005,
    Range = 0x0006,
    DontCare = -1
}

internal enum TwType : short
{
    Int8 = 0x0000,
    Int16 = 0x0001,
    Int32 = 0x0002,
    UInt8 = 0x0003,
    UInt16 = 0x0004,
    UInt32 = 0x0005,
    Bool = 0x0006,
    Fix32 = 0x0007,
    Frame = 0x0008,
    Str32 = 0x0009,
    Str64 = 0x000a,
    Str128 = 0x000b,
    Str255 = 0x000c,
    Str1024 = 0x000d,
    Str512 = 0x000e
}

internal enum TwCap : short
{
    XferCount = 0x0001,
    ICompression = 0x0100,
    IPixelType = 0x0101,
    IUnits = 0x0102,
    IXferMech = 0x0103
}

[StructLayout(LayoutKind.Sequential, Pack = 2, CharSet = CharSet.Ansi)]
internal class TwIdentity
{
    public IntPtr Id;
    public TwVersion Version;
    public short ProtocolMajor;
    public short ProtocolMinor;
    public int SupportedGroups;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)]
    public string Manufacturer = string.Empty;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)]
    public string ProductFamily = string.Empty;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)]
    public string ProductName = string.Empty;
}

[StructLayout(LayoutKind.Sequential, Pack = 2, CharSet = CharSet.Ansi)]
internal struct TwVersion
{
    public short MajorNum;
    public short MinorNum;
    public short Language;
    public short Country;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)]
    public string Info;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal class TwUserInterface
{
    public short ShowUI;
    public short ModalUI;
    public IntPtr ParentHand;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal class TwStatus
{
    public short ConditionCode;
    public short Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal struct TwEvent
{
    public IntPtr EventPtr;
    public short Message;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal class TwImageInfo
{
    public int XResolution;
    public int YResolution;
    public int ImageWidth;
    public int ImageLength;
    public short SamplesPerPixel;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public short[]? BitsPerSample;
    public short BitsPerPixel;
    public short Planar;
    public short PixelType;
    public short Compression;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal class TwPendingXfers
{
    public short Count;
    public int EOJ;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal struct TwFix32
{
    public short Whole;
    public ushort Frac;

    public float ToFloat() => Whole + (Frac / 65536.0f);

    public void FromFloat(float f)
    {
        var i = (int)((f * 65536.0f) + 0.5f);
        Whole = (short)(i >> 16);
        Frac = (ushort)(i & 0x0000ffff);
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct WinMsg
{
    public IntPtr hwnd;
    public int message;
    public IntPtr wParam;
    public IntPtr lParam;
    public int time;
    public int x;
    public int y;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal class BitmapInfoHeader
{
    public int biSize;
    public int biWidth;
    public int biHeight;
    public short biPlanes;
    public short biBitCount;
    public int biCompression;
    public int biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public int biClrUsed;
    public int biClrImportant;
}
