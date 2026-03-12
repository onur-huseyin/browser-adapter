namespace KosmosAdapterV2.Core.Models;

public sealed record ImageInfo(
    int Width,
    int Height,
    int BitsPerPixel,
    long SizeInBytes
);
