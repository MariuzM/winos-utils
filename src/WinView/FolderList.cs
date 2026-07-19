using WinView.Native;

namespace WinView;

// The sibling images of the one that was opened, in Explorer's order, so the arrow keys step
// through a folder the way the user expects.
internal sealed class FolderList
{
    // The set GDI+ can actually decode. HEIC, WebP and AVIF are absent on purpose - they would
    // appear in the list and then fail to load.
    private static readonly string[] Extensions =
    [
        ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".tif", ".tiff", ".ico",
    ];

    private readonly string[] _files;
    private int _index;

    private FolderList(string[] files, int index)
    {
        _files = files;
        _index = index;
    }

    public int Count => _files.Length;

    public int Position => _index + 1;

    public string Current => _files[_index];

    public static FolderList For(string path)
    {
        string full = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(full);

        // A file with no directory, an unreadable directory, or one that vanished between the open
        // and the scan all degrade to a single-entry list rather than failing the open.
        if (string.IsNullOrEmpty(directory))
            return new FolderList([full], 0);

        string[] files;
        try
        {
            files = Directory.EnumerateFiles(directory)
                .Where(IsSupported)
                .ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new FolderList([full], 0);
        }

        if (files.Length == 0)
            return new FolderList([full], 0);

        Array.Sort(files, static (a, b) => Win32.StrCmpLogical(Path.GetFileName(a), Path.GetFileName(b)));

        int index = Array.FindIndex(files, f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase));
        return new FolderList(files, index < 0 ? 0 : index);
    }

    private static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    // Wraps at both ends: stepping past the last image returns to the first, which is what every
    // other viewer does.
    public string Step(int delta)
    {
        if (_files.Length == 0)
            return Current;

        _index = ((_index + delta) % _files.Length + _files.Length) % _files.Length;
        return Current;
    }
}
