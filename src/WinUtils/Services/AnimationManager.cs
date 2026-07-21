using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinUtils.Services;

public static class AnimationManager
{
    private const uint SpiGetAnimation = 0x0048;
    private const uint SpiSetAnimation = 0x0049;
    private const uint SpiSetMenuAnimation = 0x1003;
    private const uint SpiSetUiEffects = 0x103F;
    private const uint UpdateIniFile = 0x0001;
    private const uint SendChange = 0x0002;
    private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    [StructLayout(LayoutKind.Sequential)]
    private struct AnimationInfo
    {
        public uint Size;
        public int MinAnimate;
    }

    public static bool IsEnabled()
    {
        var info = new AnimationInfo { Size = (uint)Marshal.SizeOf<AnimationInfo>() };
        if (!GetSystemParametersInfo(SpiGetAnimation, info.Size, ref info, 0))
            throw new InvalidOperationException("Windows did not report the current animation setting.");

        return info.MinAnimate != 0;
    }

    public static void SetEnabled(bool enabled)
    {
        var info = new AnimationInfo
        {
            Size = (uint)Marshal.SizeOf<AnimationInfo>(),
            MinAnimate = enabled ? 1 : 0,
        };

        if (!SetAnimationInfo(SpiSetAnimation, info.Size, ref info, UpdateIniFile | SendChange))
            throw new InvalidOperationException("Windows rejected the animation setting change.");

        SetSystemParametersInfo(SpiSetMenuAnimation, 0, enabled ? 1 : 0, UpdateIniFile | SendChange);
        SetSystemParametersInfo(SpiSetUiEffects, 0, enabled ? 1 : 0, UpdateIniFile | SendChange);

        using var key = Registry.CurrentUser.CreateSubKey(ExplorerAdvanced, true);
        key.SetValue("TaskbarAnimations", enabled ? 1 : 0, RegistryValueKind.DWord);
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemParametersInfo(uint action, uint param, ref AnimationInfo value, uint winIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetAnimationInfo(uint action, uint param, ref AnimationInfo value, uint winIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemParametersInfo(uint action, uint param, nint value, uint winIni);
}
