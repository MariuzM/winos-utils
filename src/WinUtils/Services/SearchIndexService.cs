using System.Diagnostics;
using Microsoft.Win32;

namespace WinUtils.Services;

public static class SearchIndexService
{
    private const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\WSearch";

    public static bool IsEnabled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(ServiceKey, false);
        if (key is null)
            return false;

        return key.GetValue("Start") is int start && start != 4;
    }

    public static void SetEnabled(bool enabled)
    {
        using (var key = Registry.LocalMachine.OpenSubKey(ServiceKey, true))
        {
            if (key is null)
                throw new InvalidOperationException("The Windows Search service (WSearch) is not installed.");

            key.SetValue("Start", enabled ? 2 : 4, RegistryValueKind.DWord);
            if (enabled)
                key.SetValue("DelayedAutostart", 1, RegistryValueKind.DWord);
        }

        RunSc(enabled ? "start WSearch" : "stop WSearch");
    }

    private static void RunSc(string arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var p = Process.Start(info);
        p?.WaitForExit(15000);
    }
}
