using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KosmosAdapterV2.Core.Enums;
using KosmosAdapterV2.Core.Interfaces;
using KosmosAdapterV2.Core.Models;
using Microsoft.Extensions.Logging;

namespace KosmosAdapterV2.Infrastructure.Services;

public sealed class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GlobalFree(IntPtr handle);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern int SetDIBitsToDevice(
        IntPtr hdc, int xdst, int ydst, int width, int height,
        int xsrc, int ysrc, int start, int lines,
        IntPtr bitsptr, IntPtr bmiptr, int color);

    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }

    public Bitmap Crop(Bitmap source, CropRegion region)
    {
        try
        {
            var rect = region.ToRectangle();
            
            rect.Width = Math.Min(rect.Width, source.Width - rect.X);
            rect.Height = Math.Min(rect.Height, source.Height - rect.Y);

            var cropped = new Bitmap(rect.Width, rect.Height);
            
            using var graphics = Graphics.FromImage(cropped);
            graphics.DrawImage(source, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);

            _logger.LogDebug("Image cropped to {Width}x{Height}", rect.Width, rect.Height);
            return cropped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cropping image");
            throw;
        }
    }

    public Bitmap Rotate(Bitmap source, float angle)
    {
        try
        {
            var rotated = new Bitmap(source.Width, source.Height);
            
            using var graphics = Graphics.FromImage(rotated);
            graphics.TranslateTransform(source.Width / 2f, source.Height / 2f);
            graphics.RotateTransform(angle);
            graphics.TranslateTransform(-source.Width / 2f, -source.Height / 2f);
            graphics.DrawImage(source, new Point(0, 0));

            _logger.LogDebug("Image rotated by {Angle} degrees", angle);
            return rotated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating image");
            throw;
        }
    }

    public Bitmap Resize(Bitmap source, int width, int height, bool maintainAspectRatio = true)
    {
        try
        {
            int newWidth = width;
            int newHeight = height;

            if (maintainAspectRatio)
            {
                var ratioX = (double)width / source.Width;
                var ratioY = (double)height / source.Height;
                var ratio = Math.Min(ratioX, ratioY);

                newWidth = (int)(source.Width * ratio);
                newHeight = (int)(source.Height * ratio);
            }

            var resized = new Bitmap(newWidth, newHeight);
            
            using var graphics = Graphics.FromImage(resized);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, 0, 0, newWidth, newHeight);

            _logger.LogDebug("Image resized to {Width}x{Height}", newWidth, newHeight);
            return resized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resizing image");
            throw;
        }
    }

    public Bitmap Scale(Bitmap source, float scale)
    {
        try
        {
            var newWidth = (int)(source.Width * scale);
            var newHeight = (int)(source.Height * scale);

            var scaled = new Bitmap(newWidth, newHeight);
            
            using var graphics = Graphics.FromImage(scaled);
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, newWidth, newHeight), 
                new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);

            _logger.LogDebug("Image scaled by {Scale} to {Width}x{Height}", scale, newWidth, newHeight);
            return scaled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scaling image");
            throw;
        }
    }

    public byte[] ToByteArray(Image image, ImageOutputFormat format = ImageOutputFormat.Jpeg)
    {
        try
        {
            using var ms = new MemoryStream();
            image.Save(ms, GetSystemImageFormat(format));
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting image to byte array");
            throw;
        }
    }

    public string ToBase64(Image image, ImageOutputFormat format = ImageOutputFormat.Jpeg)
    {
        var bytes = ToByteArray(image, format);
        var mimeType = GetMimeType(format);
        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    public Bitmap FromDibHandle(IntPtr dibHandle)
    {
        try
        {
            var bitmapPointer = GlobalLock(dibHandle);
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
                    SetDIBitsToDevice(
                        hdc, 0, 0,
                        bitmapInfo.biWidth, bitmapInfo.biHeight,
                        0, 0, 0, bitmapInfo.biHeight,
                        pixelInfoIntPointer, bitmapPointer, 0);
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

            GlobalUnlock(dibHandle);
            GlobalFree(dibHandle);

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting DIB to bitmap");
            throw;
        }
    }

    public void SaveImage(Image image, string filePath, ImageOutputFormat format = ImageOutputFormat.Jpeg)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            image.Save(filePath, GetSystemImageFormat(format));
            _logger.LogInformation("Image saved to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving image to {FilePath}", filePath);
            throw;
        }
    }

    public ImageInfo GetImageInfo(Image image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        
        var bitsPerPixel = Image.GetPixelFormatSize(image.PixelFormat);
        
        return new ImageInfo(
            image.Width,
            image.Height,
            bitsPerPixel,
            ms.Length
        );
    }

    private static System.Drawing.Imaging.ImageFormat GetSystemImageFormat(ImageOutputFormat format) => format switch
    {
        ImageOutputFormat.Jpeg => System.Drawing.Imaging.ImageFormat.Jpeg,
        ImageOutputFormat.Png => System.Drawing.Imaging.ImageFormat.Png,
        ImageOutputFormat.Bmp => System.Drawing.Imaging.ImageFormat.Bmp,
        ImageOutputFormat.Tiff => System.Drawing.Imaging.ImageFormat.Tiff,
        ImageOutputFormat.Gif => System.Drawing.Imaging.ImageFormat.Gif,
        _ => System.Drawing.Imaging.ImageFormat.Jpeg
    };

    private static string GetMimeType(ImageOutputFormat format) => format switch
    {
        ImageOutputFormat.Jpeg => "image/jpeg",
        ImageOutputFormat.Png => "image/png",
        ImageOutputFormat.Bmp => "image/bmp",
        ImageOutputFormat.Tiff => "image/tiff",
        ImageOutputFormat.Gif => "image/gif",
        _ => "image/jpeg"
    };

    private static float PpmToDpi(double pixelsPerMeter)
    {
        var pixelsPerMillimeter = pixelsPerMeter / 1000.0;
        var dotsPerInch = pixelsPerMillimeter * 25.4;
        return (float)Math.Round(dotsPerInch, 2);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private class BitmapInfoHeader
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
}
