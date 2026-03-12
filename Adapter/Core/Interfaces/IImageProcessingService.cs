using KosmosAdapterV2.Core.Models;
using KosmosAdapterV2.Core.Enums;

namespace KosmosAdapterV2.Core.Interfaces;

public interface IImageProcessingService
{
    Bitmap Crop(Bitmap source, CropRegion region);
    Bitmap Rotate(Bitmap source, float angle);
    Bitmap Resize(Bitmap source, int width, int height, bool maintainAspectRatio = true);
    Bitmap Scale(Bitmap source, float scale);
    byte[] ToByteArray(Image image, ImageOutputFormat format = ImageOutputFormat.Jpeg);
    string ToBase64(Image image, ImageOutputFormat format = ImageOutputFormat.Jpeg);
    Bitmap FromDibHandle(IntPtr dibHandle);
    void SaveImage(Image image, string filePath, ImageOutputFormat format = ImageOutputFormat.Jpeg);
    ImageInfo GetImageInfo(Image image);
}
