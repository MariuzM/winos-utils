using WinView.Native;
using static WinView.Native.Win32Const;

namespace WinView;

// One decoded image. GDI+ owns the pixels; this type owns the GDI+ handle.
internal sealed unsafe class ImageDocument : IDisposable
{
    private IntPtr _image;

    private ImageDocument(IntPtr image, int width, int height, string path)
    {
        _image = image;
        Width = width;
        Height = height;
        Path = path;
    }

    public IntPtr Handle => _image;

    public int Width { get; }

    public int Height { get; }

    public string Path { get; }

    public static ImageDocument? Load(string path)
    {
        IntPtr image;
        fixed (char* name = path)
        {
            if (Win32.GdipCreateBitmapFromFile(name, &image) != 0 || image == IntPtr.Zero)
                return null;
        }

        // Rotation happens before the dimensions are read: a portrait phone photo is stored
        // landscape with an orientation tag, so measuring first would give transposed dimensions
        // and the fit-to-window maths would be wrong.
        ApplyExifOrientation(image);

        uint width;
        uint height;
        if (Win32.GdipGetImageWidth(image, &width) != 0 || Win32.GdipGetImageHeight(image, &height) != 0)
        {
            Win32.GdipDisposeImage(image);
            return null;
        }

        if (width == 0 || height == 0)
        {
            Win32.GdipDisposeImage(image);
            return null;
        }

        return new ImageDocument(image, (int)width, (int)height, path);
    }

    // EXIF orientation, tag 0x0112. Absent on most PNGs and screenshots, present on nearly every
    // photo taken with a phone - without this they display sideways.
    private static void ApplyExifOrientation(IntPtr image)
    {
        uint size;
        if (Win32.GdipGetPropertyItemSize(image, PropertyTagOrientation, &size) != 0)
            return;

        // The item is a header plus a two-byte value. A wildly larger size means something other
        // than what is expected, so leave the image alone rather than stack-allocating on trust.
        if (size < sizeof(PropertyItem) || size > 256)
            return;

        byte* buffer = stackalloc byte[(int)size];
        var item = (PropertyItem*)buffer;

        if (Win32.GdipGetPropertyItem(image, PropertyTagOrientation, size, item) != 0)
            return;

        // Type 3 is PropertyTagTypeShort. Anything else is a malformed tag.
        if (item->type != 3 || item->length < 2 || item->value is null)
            return;

        int rotation = *(ushort*)item->value switch
        {
            2 => RotateNoneFlipX,
            3 => Rotate180FlipNone,
            4 => Rotate180FlipX,
            5 => Rotate90FlipX,
            6 => Rotate90FlipNone,
            7 => Rotate270FlipX,
            8 => Rotate270FlipNone,
            _ => RotateNoneFlipNone,
        };

        if (rotation != RotateNoneFlipNone)
            Win32.GdipImageRotateFlip(image, rotation);
    }

    public void Dispose()
    {
        if (_image != IntPtr.Zero)
        {
            Win32.GdipDisposeImage(_image);
            _image = IntPtr.Zero;
        }
    }
}
