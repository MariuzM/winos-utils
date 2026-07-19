using System.Runtime.InteropServices;
using WinView.Native;
using static WinView.Native.Win32Const;

namespace WinView;

internal static unsafe class ViewerWindow
{
    private const string ClassName = "WinView.Viewer";
    private const uint SRCCOPY = 0x00CC0020;

    // Matches the dark neutral Photos uses. A mid-grey rather than black so that dark images still
    // read as having an edge.
    private const uint Background = 0xFF1E1E1E;

    private const double MinZoom = 0.02;
    private const double MaxZoom = 64.0;
    private const double ZoomStep = 1.25;

    private static IntPtr _hwnd;
    private static ImageDocument? _document;
    private static FolderList? _folder;

    private static double _zoom = 1.0;
    private static double _panX;
    private static double _panY;
    private static bool _fitToWindow = true;

    private static bool _panning;
    private static POINT _dragOrigin;
    private static double _dragPanX;
    private static double _dragPanY;

    public static bool Create(string? initialPath)
    {
        IntPtr namePtr = Marshal.StringToHGlobalUni(ClassName);

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)sizeof(WNDCLASSEXW),
            style = 0x0008, // CS_DBLCLKS - double-click toggles fit and 1:1
            lpfnWndProc = &WndProc,
            hInstance = Win32.GetModuleHandle(null),
            hCursor = Win32.LoadCursor(IntPtr.Zero, new IntPtr(IDC_ARROW)),
            hbrBackground = IntPtr.Zero,
            lpszClassName = namePtr,
        };

        if (Win32.RegisterClassEx(ref wc) == 0)
            return false;

        _hwnd = Win32.CreateWindowEx(
            WS_EX_ACCEPTFILES, ClassName, "WinView", WS_OVERLAPPEDWINDOW,
            CW_USEDEFAULT, CW_USEDEFAULT, 1100, 750,
            IntPtr.Zero, IntPtr.Zero, Win32.GetModuleHandle(null), IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            return false;

        Win32.DragAcceptFiles(_hwnd, true);

        if (!string.IsNullOrEmpty(initialPath))
            Open(initialPath);

        // Maximised by default. The 1100x750 passed to CreateWindowEx stays as the restored size,
        // so un-maximising gives a sensible window rather than a default-sized one.
        Win32.ShowWindow(_hwnd, SW_SHOWMAXIMIZED);
        return true;
    }

    public static void RunMessageLoop()
    {
        while (true)
        {
            int result = Win32.GetMessage(out MSG msg, IntPtr.Zero, 0, 0);
            if (result is 0 or -1)
                break;

            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessage(ref msg);
        }
    }

    // ---- Document ---------------------------------------------------------------

    private static void Open(string path)
    {
        _folder = FolderList.For(path);
        Load(path);
    }

    private static void Load(string path)
    {
        ImageDocument? loaded = ImageDocument.Load(path);

        // A file that fails to decode must not blank the viewer - keep showing whatever was on
        // screen and say so in the title, so stepping through a folder past a corrupt file works.
        if (loaded is null)
        {
            Win32.SetWindowText(_hwnd, $"{Path.GetFileName(path)} - could not be opened");
            return;
        }

        _document?.Dispose();
        _document = loaded;

        _fitToWindow = true;
        ResetView();
        UpdateTitle();
        Win32.InvalidateRect(_hwnd, IntPtr.Zero, false);
    }

    private static void Step(int delta)
    {
        if (_folder is null || _folder.Count <= 1)
            return;

        Load(_folder.Step(delta));
    }

    private static void UpdateTitle()
    {
        if (_document is null)
        {
            Win32.SetWindowText(_hwnd, "WinView");
            return;
        }

        string name = Path.GetFileName(_document.Path);
        string position = _folder is { Count: > 1 } f ? $"  ({f.Position}/{f.Count})" : string.Empty;
        Win32.SetWindowText(
            _hwnd, $"{name}  -  {_document.Width} x {_document.Height}  -  {_zoom * 100:0}%{position}");
    }

    // ---- View maths -------------------------------------------------------------

    private static void ClientSize(out int width, out int height)
    {
        Win32.GetClientRect(_hwnd, out RECT client);
        width = Math.Max(1, client.Width);
        height = Math.Max(1, client.Height);
    }

    private static double FitZoom()
    {
        if (_document is null)
            return 1.0;

        ClientSize(out int cw, out int ch);

        // Never scale up to fit: a 32x32 icon blown across a 1440p window looks broken. Small
        // images sit at 1:1 in the middle instead.
        return Math.Min(1.0, Math.Min((double)cw / _document.Width, (double)ch / _document.Height));
    }

    private static void ResetView()
    {
        _zoom = _fitToWindow ? FitZoom() : 1.0;
        Centre();
    }

    private static void Centre()
    {
        if (_document is null)
            return;

        ClientSize(out int cw, out int ch);
        _panX = (cw - _document.Width * _zoom) / 2.0;
        _panY = (ch - _document.Height * _zoom) / 2.0;
    }

    private static void SetZoom(double zoom, int anchorX, int anchorY)
    {
        if (_document is null)
            return;

        double clamped = Math.Clamp(zoom, MinZoom, MaxZoom);

        // Keep the image point under the anchor fixed, so wheel-zoom homes in on the cursor rather
        // than drifting toward a corner.
        double imageX = (anchorX - _panX) / _zoom;
        double imageY = (anchorY - _panY) / _zoom;

        _zoom = clamped;
        _panX = anchorX - imageX * _zoom;
        _panY = anchorY - imageY * _zoom;

        ClampPan();
        UpdateTitle();
        Win32.InvalidateRect(_hwnd, IntPtr.Zero, false);
    }

    // Keeps the image from being dragged off-screen. When it is smaller than the window on an axis
    // it stays centred on that axis instead.
    private static void ClampPan()
    {
        if (_document is null)
            return;

        ClientSize(out int cw, out int ch);
        double drawnWidth = _document.Width * _zoom;
        double drawnHeight = _document.Height * _zoom;

        _panX = drawnWidth <= cw ? (cw - drawnWidth) / 2.0 : Math.Clamp(_panX, cw - drawnWidth, 0);
        _panY = drawnHeight <= ch ? (ch - drawnHeight) / 2.0 : Math.Clamp(_panY, ch - drawnHeight, 0);
    }

    // ---- Painting ---------------------------------------------------------------

    private static void Paint()
    {
        IntPtr hdc = Win32.BeginPaint(_hwnd, out PAINTSTRUCT ps);
        try
        {
            ClientSize(out int cw, out int ch);

            // Drawn to a back buffer and blitted once: painting GDI+ straight to the window DC
            // flickers badly while panning.
            IntPtr memoryDc = Win32.CreateCompatibleDC(hdc);
            IntPtr bitmap = Win32.CreateCompatibleBitmap(hdc, cw, ch);
            IntPtr previous = Win32.SelectObject(memoryDc, bitmap);

            try
            {
                IntPtr graphics;
                if (Win32.GdipCreateFromHDC(memoryDc, &graphics) == 0)
                {
                    try
                    {
                        Win32.GdipGraphicsClear(graphics, Background);
                        DrawImage(graphics);
                    }
                    finally
                    {
                        Win32.GdipDeleteGraphics(graphics);
                    }
                }

                Win32.BitBlt(hdc, 0, 0, cw, ch, memoryDc, 0, 0, SRCCOPY);
            }
            finally
            {
                Win32.SelectObject(memoryDc, previous);
                Win32.DeleteObject(bitmap);
                Win32.DeleteDC(memoryDc);
            }
        }
        finally
        {
            Win32.EndPaint(_hwnd, ref ps);
        }
    }

    private static void DrawImage(IntPtr graphics)
    {
        if (_document is null)
            return;

        // Bicubic when shrinking, nearest-neighbour past 1:1. Interpolating an upscale would blur
        // exactly the case where someone is zooming in to inspect individual pixels.
        Win32.GdipSetInterpolationMode(
            graphics, _zoom < 1.0 ? InterpolationModeHighQualityBicubic : InterpolationModeNearestNeighbor);
        Win32.GdipSetPixelOffsetMode(graphics, PixelOffsetModeHighQuality);

        int width = Math.Max(1, (int)Math.Round(_document.Width * _zoom));
        int height = Math.Max(1, (int)Math.Round(_document.Height * _zoom));

        Win32.GdipDrawImageRectI(
            graphics, _document.Handle, (int)Math.Round(_panX), (int)Math.Round(_panY), width, height);
    }

    // ---- Input ------------------------------------------------------------------

    private static void OnKey(int key)
    {
        switch (key)
        {
            case VK_ESCAPE:
                Win32.DestroyWindow(_hwnd);
                break;

            case VK_RIGHT or VK_DOWN or VK_NEXT:
                Step(1);
                break;

            case VK_LEFT or VK_UP or VK_PRIOR:
                Step(-1);
                break;

            case VK_OEM_PLUS or VK_ADD:
                ZoomFromCentre(ZoomStep);
                break;

            case VK_OEM_MINUS or VK_SUBTRACT:
                ZoomFromCentre(1.0 / ZoomStep);
                break;

            case VK_0:
                _fitToWindow = true;
                ResetView();
                UpdateTitle();
                Win32.InvalidateRect(_hwnd, IntPtr.Zero, false);
                break;

            case VK_1:
                _fitToWindow = false;
                _zoom = 1.0;
                Centre();
                UpdateTitle();
                Win32.InvalidateRect(_hwnd, IntPtr.Zero, false);
                break;
        }
    }

    private static void ZoomFromCentre(double factor)
    {
        ClientSize(out int cw, out int ch);
        _fitToWindow = false;
        SetZoom(_zoom * factor, cw / 2, ch / 2);
    }

    private static void OnDropFiles(IntPtr drop)
    {
        try
        {
            char* buffer = stackalloc char[1024];
            if (Win32.DragQueryFile(drop, 0, buffer, 1024) > 0)
                Open(new string(buffer));
        }
        finally
        {
            Win32.DragFinish(drop);
        }
    }

    [UnmanagedCallersOnly]
    private static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case WM_ERASEBKGND:
                    // Handled by the back buffer; letting the system erase would flicker.
                    return new IntPtr(1);

                case WM_PAINT:
                    Paint();
                    return IntPtr.Zero;

                case WM_SIZE:
                    if (_fitToWindow)
                        ResetView();
                    else
                        ClampPan();

                    UpdateTitle();
                    Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
                    return IntPtr.Zero;

                case WM_KEYDOWN:
                    OnKey((int)wParam);
                    return IntPtr.Zero;

                case WM_MOUSEWHEEL:
                {
                    int delta = Win32.HiWord(wParam);
                    _fitToWindow = false;

                    // Wheel coordinates are screen-relative, unlike every other mouse message.
                    var point = new POINT { X = Win32.LoWord(lParam), Y = Win32.HiWord(lParam) };
                    Win32.ScreenToClient(hwnd, ref point);

                    SetZoom(_zoom * (delta > 0 ? ZoomStep : 1.0 / ZoomStep), point.X, point.Y);
                    return IntPtr.Zero;
                }

                case WM_LBUTTONDOWN:
                    _panning = true;
                    _dragOrigin = new POINT { X = Win32.LoWord(lParam), Y = Win32.HiWord(lParam) };
                    _dragPanX = _panX;
                    _dragPanY = _panY;
                    Win32.SetCapture(hwnd);
                    Win32.SetCursor(Win32.LoadCursor(IntPtr.Zero, new IntPtr(IDC_SIZEALL)));
                    return IntPtr.Zero;

                case WM_MOUSEMOVE:
                    if (_panning)
                    {
                        _panX = _dragPanX + (Win32.LoWord(lParam) - _dragOrigin.X);
                        _panY = _dragPanY + (Win32.HiWord(lParam) - _dragOrigin.Y);
                        ClampPan();
                        Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
                    }
                    return IntPtr.Zero;

                case WM_LBUTTONUP:
                    if (_panning)
                    {
                        _panning = false;
                        Win32.ReleaseCapture();
                        Win32.SetCursor(Win32.LoadCursor(IntPtr.Zero, new IntPtr(IDC_ARROW)));
                    }
                    return IntPtr.Zero;

                case WM_LBUTTONDBLCLK:
                    _fitToWindow = !_fitToWindow;
                    ResetView();
                    UpdateTitle();
                    Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
                    return IntPtr.Zero;

                case WM_DROPFILES:
                    OnDropFiles(wParam);
                    return IntPtr.Zero;

                case WM_DPICHANGED:
                {
                    // lParam carries the rectangle Windows wants the window moved to; ignoring it
                    // leaves the window the wrong physical size after a monitor change.
                    var suggested = (RECT*)lParam;
                    Win32.SetWindowPos(
                        hwnd, IntPtr.Zero, suggested->Left, suggested->Top,
                        suggested->Width, suggested->Height, SWP_NOZORDER | SWP_NOACTIVATE);

                    if (_fitToWindow)
                        ResetView();

                    Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
                    return IntPtr.Zero;
                }

                case WM_DESTROY:
                    _document?.Dispose();
                    _document = null;
                    Win32.PostQuitMessage(0);
                    return IntPtr.Zero;
            }
        }
        catch
        {
        }

        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
