using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WinUtils.Services;

public static class PowerPlanService
{
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    public static bool IsHighPerformance()
    {
        var (_, output) = RunPowerCfg("/getactivescheme");
        return output.Contains(HighPerformanceGuid, StringComparison.OrdinalIgnoreCase)
            || output.Contains("(High performance)", StringComparison.OrdinalIgnoreCase);
    }

    public static void SetHighPerformance(bool enabled)
    {
        if (!enabled)
        {
            var (balancedExit, balancedOutput) = RunPowerCfg($"/setactive {BalancedGuid}");
            if (balancedExit != 0)
                throw new InvalidOperationException($"powercfg could not activate the Balanced plan. {balancedOutput}".Trim());

            return;
        }

        var (exit, _) = RunPowerCfg($"/setactive {HighPerformanceGuid}");
        if (exit == 0)
            return;

        var (dupExit, dupOutput) = RunPowerCfg($"/duplicatescheme {HighPerformanceGuid}");
        var match = Regex.Match(dupOutput, "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase);
        if (dupExit != 0 || !match.Success)
            throw new InvalidOperationException($"powercfg could not create the High performance plan. {dupOutput}".Trim());

        var (activateExit, activateOutput) = RunPowerCfg($"/setactive {match.Value}");
        if (activateExit != 0)
            throw new InvalidOperationException($"powercfg could not activate the High performance plan. {activateOutput}".Trim());
    }

    private static (int ExitCode, string Output) RunPowerCfg(string arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var p = Process.Start(info);
        if (p is null)
            return (-1, "powercfg could not be started.");

        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        if (!p.WaitForExit(15000))
            return (-1, "powercfg timed out.");

        return (p.ExitCode, output);
    }
}
