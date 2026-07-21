using Microsoft.Win32;

namespace WinUtils.Services;

public static class DeviceMetadataService
{
    private const string MetadataPolicy = @"SOFTWARE\Policies\Microsoft\Windows\Device Metadata";

    public static bool IsAutoDownloadEnabled()
    {
        return GetDword(MetadataPolicy, "PreventDeviceMetadataFromNetwork") != 1;
    }

    public static void SetAutoDownloadEnabled(bool enabled)
    {
        SetDword(MetadataPolicy, "PreventDeviceMetadataFromNetwork", enabled ? 0 : 1);
    }

    private static int? GetDword(string sub, string name)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(sub, false);
        return key?.GetValue(name) is int value ? value : null;
    }

    private static void SetDword(string sub, string name, int value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(sub, true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }
}
