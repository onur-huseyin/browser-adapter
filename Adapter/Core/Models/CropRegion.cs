namespace KosmosAdapterV2.Core.Models;

public sealed record CropRegion(int X, int Y, int Width, int Height)
{
    public Rectangle ToRectangle() => new(X, Y, Width, Height);
    
    public static CropRegion FromRectangle(Rectangle rect) => 
        new(rect.X, rect.Y, rect.Width, rect.Height);
}
