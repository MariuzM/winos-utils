using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinUtils.Services;

public static class WindowShadowManager
{
    private const uint SpiGetDropShadow = 0x1024;
    private const uint SpiSetDropShadow = 0x1025;
    private const uint UpdateIniFile = 0x0001;
    private const uint SendChange = 0x0002;
    private const string VisualEffectsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
    private const int VisualFxCustom = 3;

    public static bool IsEnabled()
    {
        int enabled = 1;
        if (!GetSystemParametersInfo(SpiGetDropShadow, 0, ref enabled, 0))
            throw new InvalidOperationException("Windows did not report the current shadow setting.");

        return enabled != 0;
    }

    public static void SetEnabled(bool enabled)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(VisualEffectsKey, true))
            key.SetValue("VisualFXSetting", VisualFxCustom, RegistryValueKind.DWord);

        if (!SetSystemParametersInfo(SpiSetDropShadow, 0, enabled ? 1 : 0, UpdateIniFile | SendChange))
            throw new InvalidOperationException("Windows rejected the shadow setting change.");
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemParametersInfo(uint action, uint param, ref int value, uint winIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemParametersInfo(uint action, uint param, nint value, uint winIni);
}
