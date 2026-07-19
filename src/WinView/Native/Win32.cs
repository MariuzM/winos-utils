using System.Runtime.InteropServices;

namespace WinView.Native;

internal static unsafe partial class Win32
{
    private const string User32 = "user32.dll";
    private const string Gdi32 = "gdi32.dll";
    private const string Shell32 = "shell32.dll";
    private const string Shlwapi = "shlwapi.dll";
    private const string Kernel32 = "kernel32.dll";
    private const string GdiPlus = "gdiplus.dll";

    // ---- Module ----------------------------------------------------------------

    [LibraryImport(Kernel32, EntryPoint = "AttachConsole")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachConsole(uint dwProcessId);

    [LibraryImport(Kernel32, EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr GetModuleHandle(string? lpModuleName);

    // ---- Window class & creation ------------------------------------------------

    [LibraryImport(User32, EntryPoint = "RegisterClassExW")]
    public static partial ushort RegisterClassEx(ref WNDCLASSEXW lpwcx);

    [LibraryImport(User32, EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [LibraryImport(User32, EntryPoint = "DefWindowProcW")]
    public static partial IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport(User32, EntryPoint = "DestroyWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(IntPtr hWnd);

    [LibraryImport(User32, EntryPoint = "LoadCursorW")]
    public static partial IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [LibraryImport(User32, EntryPoint = "SetCursor")]
    public static partial IntPtr SetCursor(IntPtr hCursor);

    [LibraryImport(User32, EntryPoint = "SetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowText(IntPtr hWnd, string lpString);

    [LibraryImport(User32, EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    // ---- Message loop -----------------------------------------------------------

    [LibraryImport(User32, EntryPoint = "GetMessageW")]
    public static partial int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport(User32, EntryPoint = "TranslateMessage")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(ref MSG lpMsg);

    [LibraryImport(User32, EntryPoint = "DispatchMessageW")]
    public static partial IntPtr DispatchMessage(ref MSG lpMsg);

    [LibraryImport(User32, EntryPoint = "PostQuitMessage")]
    public static partial void PostQuitMessage(int nExitCode);

    // ---- Window state -----------------------------------------------------------

    [LibraryImport(User32, EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport(User32, EntryPoint = "SetWindowPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport(User32, EntryPoint = "GetClientRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport(User32, EntryPoint = "InvalidateRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

    [LibraryImport(User32, EntryPoint = "ScreenToClient")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [LibraryImport(User32, EntryPoint = "SetCapture")]
    public static partial IntPtr SetCapture(IntPtr hWnd);

    [LibraryImport(User32, EntryPoint = "ReleaseCapture")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReleaseCapture();

    // ---- Painting ---------------------------------------------------------------

    [LibraryImport(User32, EntryPoint = "BeginPaint")]
    public static partial IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [LibraryImport(User32, EntryPoint = "EndPaint")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [LibraryImport(Gdi32, EntryPoint = "CreateCompatibleDC")]
    public static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    [LibraryImport(Gdi32, EntryPoint = "CreateCompatibleBitmap")]
    public static partial IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [LibraryImport(Gdi32, EntryPoint = "SelectObject")]
    public static partial IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [LibraryImport(Gdi32, EntryPoint = "DeleteDC")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(IntPtr hdc);

    [LibraryImport(Gdi32, EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(IntPtr ho);

    [LibraryImport(Gdi32, EntryPoint = "BitBlt")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(
        IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

    // ---- Shell ------------------------------------------------------------------

    [LibraryImport(Shell32, EntryPoint = "DragAcceptFiles")]
    public static partial void DragAcceptFiles(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAccept);

    [LibraryImport(Shell32, EntryPoint = "DragQueryFileW")]
    public static partial uint DragQueryFile(IntPtr hDrop, uint iFile, char* lpszFile, uint cch);

    [LibraryImport(Shell32, EntryPoint = "DragFinish")]
    public static partial void DragFinish(IntPtr hDrop);

    // Explorer's own ordering: "img2" sorts before "img10", which a plain ordinal compare gets
    // wrong and is immediately noticeable when stepping through a folder.
    [LibraryImport(Shlwapi, EntryPoint = "StrCmpLogicalW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int StrCmpLogical(string psz1, string psz2);

    // ---- GDI+ -------------------------------------------------------------------

    [LibraryImport(GdiPlus, EntryPoint = "GdiplusStartup")]
    public static partial int GdiplusStartup(IntPtr* token, GdiplusStartupInput* input, IntPtr output);

    [LibraryImport(GdiPlus, EntryPoint = "GdiplusShutdown")]
    public static partial void GdiplusShutdown(IntPtr token);

    [LibraryImport(GdiPlus, EntryPoint = "GdipCreateBitmapFromFile")]
    public static partial int GdipCreateBitmapFromFile(char* filename, IntPtr* bitmap);

    [LibraryImport(GdiPlus, EntryPoint = "GdipDisposeImage")]
    public static partial int GdipDisposeImage(IntPtr image);

    [LibraryImport(GdiPlus, EntryPoint = "GdipGetImageWidth")]
    public static partial int GdipGetImageWidth(IntPtr image, uint* width);

    [LibraryImport(GdiPlus, EntryPoint = "GdipGetImageHeight")]
    public static partial int GdipGetImageHeight(IntPtr image, uint* height);

    [LibraryImport(GdiPlus, EntryPoint = "GdipImageRotateFlip")]
    public static partial int GdipImageRotateFlip(IntPtr image, int rfType);

    [LibraryImport(GdiPlus, EntryPoint = "GdipGetPropertyItemSize")]
    public static partial int GdipGetPropertyItemSize(IntPtr image, uint propId, uint* size);

    [LibraryImport(GdiPlus, EntryPoint = "GdipGetPropertyItem")]
    public static partial int GdipGetPropertyItem(IntPtr image, uint propId, uint propSize, PropertyItem* buffer);

    [LibraryImport(GdiPlus, EntryPoint = "GdipCreateFromHDC")]
    public static partial int GdipCreateFromHDC(IntPtr hdc, IntPtr* graphics);

    [LibraryImport(GdiPlus, EntryPoint = "GdipDeleteGraphics")]
    public static partial int GdipDeleteGraphics(IntPtr graphics);

    [LibraryImport(GdiPlus, EntryPoint = "GdipSetInterpolationMode")]
    public static partial int GdipSetInterpolationMode(IntPtr graphics, int interpolationMode);

    [LibraryImport(GdiPlus, EntryPoint = "GdipSetPixelOffsetMode")]
    public static partial int GdipSetPixelOffsetMode(IntPtr graphics, int pixelOffsetMode);

    [LibraryImport(GdiPlus, EntryPoint = "GdipGraphicsClear")]
    public static partial int GdipGraphicsClear(IntPtr graphics, uint colour);

    [LibraryImport(GdiPlus, EntryPoint = "GdipDrawImageRectI")]
    public static partial int GdipDrawImageRectI(IntPtr graphics, IntPtr image, int x, int y, int width, int height);

    // ---- Helpers ----------------------------------------------------------------

    public static int LoWord(IntPtr value) => (short)((long)value & 0xFFFF);

    public static int HiWord(IntPtr value) => (short)(((long)value >> 16) & 0xFFFF);
}
