using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using WindowBorderProtocol;

namespace WinUtils.Services;

public sealed record WindowBorderState(bool Supported, bool Enabled, bool Running, bool HelperAvailable);

public static class WindowBorderManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "WinUtilsWindowBorders";

    public static WindowBorderState GetState()
    {
        bool supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
        bool running = IsRunning();

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        bool registered = key?.GetValue(RunValue) is string;

        return new WindowBorderState(supported, registered || running, running, File.Exists(HelperPath));
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
            Enable();
        else
            Disable();
    }

    private static string HelperPath => Path.Combine(AppContext.BaseDirectory, Protocol.HelperFileName);

    private static void Enable()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            throw new PlatformNotSupportedException("Window border removal requires Windows 11.");

        if (!File.Exists(HelperPath))
            throw new FileNotFoundException($"{Protocol.HelperFileName} was not found beside WinUtils.exe.");

        if (!IsReady())
            StartAndWaitForReady();

        string command = $"\"{HelperPath}\"";
        if (command.Length > 260)
        {
            SignalStop();
            throw new InvalidOperationException(
                "The WinUtils folder path is too long for Windows startup registration."
            );
        }

        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, true);
            key.SetValue(RunValue, command, RegistryValueKind.String);
        }
        catch
        {
            SignalStop();
            throw;
        }
    }

    private static void Disable()
    {
        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            key?.DeleteValue(RunValue, false);

        if (SignalStop())
        {
            WaitForStopped();
            return;
        }

        if (!File.Exists(HelperPath))
            return;

        using Process? process = Process.Start(
            new ProcessStartInfo
            {
                FileName = HelperPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { "--stop" },
            }
        );

        if (process is null || !process.WaitForExit(3000) || process.ExitCode != 0)
            throw new InvalidOperationException("The window border helper could not restore the Windows default.");
    }

    private static void StartAndWaitForReady()
    {
        using Process? process = Process.Start(
            new ProcessStartInfo
            {
                FileName = HelperPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        );

        if (process is null)
            throw new InvalidOperationException("The window border helper could not be started.");

        var timeout = Stopwatch.StartNew();

        while (timeout.ElapsedMilliseconds < 3000)
        {
            if (IsReady())
                return;

            if (process.HasExited && process.ExitCode != 0)
                break;

            Thread.Sleep(20);
        }

        throw new InvalidOperationException(
            process.HasExited && process.ExitCode != 0
                ? $"The window border helper exited with code {process.ExitCode}."
                : "The window border helper did not start within three seconds."
        );
    }

    private static bool SignalStop()
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(Protocol.StopEventName, out EventWaitHandle? stopEvent))
                return false;

            using (stopEvent)
                stopEvent.Set();

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    private static bool IsRunning()
    {
        return EventExists(Protocol.StopEventName, true);
    }

    private static bool IsReady()
    {
        return EventExists(Protocol.ReadyEventName, false);
    }

    private static bool EventExists(string name, bool accessDeniedMeansExists)
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(name, out EventWaitHandle? handle))
                return false;

            handle.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return accessDeniedMeansExists;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    private static void WaitForStopped()
    {
        var timeout = Stopwatch.StartNew();

        while (timeout.ElapsedMilliseconds < 3000)
        {
            if (!IsRunning())
                return;

            Thread.Sleep(20);
        }

        throw new InvalidOperationException("The window border helper did not stop within three seconds.");
    }
}
