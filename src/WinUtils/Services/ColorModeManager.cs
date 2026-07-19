using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinUtils.Services;

public sealed record ColorModeState(bool AppsDark, bool SystemDark)
{
    public bool Dark => AppsDark && SystemDark;
    public bool Mixed => AppsDark != SystemDark;
}

public static class ColorModeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsValue = "AppsUseLightTheme";
    private const string SystemValue = "SystemUsesLightTheme";
    private const uint WindowMessageSettingChange = 0x001A;
    private const uint SendMessageAbortIfHung = 0x0002;
    private static readonly nint BroadcastWindow = new(0xFFFF);

    public static ColorModeState GetState()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, false);
        return new ColorModeState(IsDark(key, AppsValue), IsDark(key, SystemValue));
    }

    public static void SetDarkMode(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(PersonalizeKey, true);
        int value = enabled ? 0 : 1;
        key.SetValue(AppsValue, value, RegistryValueKind.DWord);
        key.SetValue(SystemValue, value, RegistryValueKind.DWord);

        SendMessageTimeout(
            BroadcastWindow,
            WindowMessageSettingChange,
            0,
            "ImmersiveColorSet",
            SendMessageAbortIfHung,
            100,
            out _
        );
    }

    private static bool IsDark(RegistryKey? key, string name)
    {
        return key?.GetValue(name) is int value && value == 0;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint SendMessageTimeout(
        nint window,
        uint message,
        nuint wParam,
        string lParam,
        uint flags,
        uint timeout,
        out nuint result
    );
}
