using System.Runtime.InteropServices;
using WindowBorderProtocol;

namespace WinBorder;

internal static unsafe class Program
{
    private const int ErrorAlreadyExists = 183;
    private const uint EventModifyState = 0x0002;
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectUncloaked = 0x8018;
    private const int ObjectIdWindow = 0;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;
    private const uint DwmWindowAttributeBorderColor = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;
    private const uint DwmColorDefault = 0xFFFFFFFF;
    private const uint Infinite = 0xFFFFFFFF;
    private const uint QueueStatusAllInput = 0x04FF;
    private const uint WaitObject0 = 0;
    private const uint WaitFailed = 0xFFFFFFFF;
    private const uint MessageWaitInputAvailable = 0x0004;
    private const uint PeekMessageRemove = 0x0001;
    private const uint WindowMessageQuit = 0x0012;
    private const uint SweepIntervalMilliseconds = 20;
    private const int SweepTicks = 25;

    private static nuint _sweepTimer;
    private static int _sweepTicksRemaining;

    private static int Main(string[] args)
    {
        if (HasArgument(args, "--stop"))
        {
            StopRunningInstance();
            SetAllBorders(DwmColorDefault);
            return 0;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return 2;

        nint mutex = Native.CreateMutex(0, true, Protocol.MutexName);
        if (mutex == 0)
            return 1;

        if (Marshal.GetLastPInvokeError() == ErrorAlreadyExists)
        {
            Native.CloseHandle(mutex);
            return 0;
        }

        nint stopEvent = 0;
        nint readyEvent = 0;
        nint foregroundHook = 0;
        nint showHook = 0;
        nint uncloakHook = 0;

        try
        {
            stopEvent = Native.CreateEvent(0, true, false, Protocol.StopEventName);
            if (stopEvent == 0)
                return 1;

            foregroundHook = HookEvent(EventSystemForeground);
            showHook = HookEvent(EventObjectShow);
            uncloakHook = HookEvent(EventObjectUncloaked);

            if (foregroundHook == 0 || showHook == 0 || uncloakHook == 0)
                return 1;

            SetAllBorders(DwmColorNone);
            readyEvent = Native.CreateEvent(0, true, true, Protocol.ReadyEventName);
            if (readyEvent == 0)
                return 1;

            WaitForStop(stopEvent);
            return 0;
        }
        finally
        {
            SetAllBorders(DwmColorDefault);

            if (uncloakHook != 0)
                Native.UnhookWinEvent(uncloakHook);

            if (showHook != 0)
                Native.UnhookWinEvent(showHook);

            if (foregroundHook != 0)
                Native.UnhookWinEvent(foregroundHook);

            if (stopEvent != 0)
                Native.CloseHandle(stopEvent);

            if (readyEvent != 0)
                Native.CloseHandle(readyEvent);

            Native.ReleaseMutex(mutex);
            Native.CloseHandle(mutex);
        }
    }

    private static bool HasArgument(string[] args, string expected)
    {
        foreach (string value in args)
        {
            if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void StopRunningInstance()
    {
        nint stopEvent = Native.OpenEvent(EventModifyState, false, Protocol.StopEventName);
        if (stopEvent == 0)
            return;

        Native.SetEvent(stopEvent);
        Native.CloseHandle(stopEvent);
    }

    private static nint HookEvent(uint eventId)
    {
        return Native.SetWinEventHook(
            eventId,
            eventId,
            0,
            &OnWinEvent,
            0,
            0,
            WinEventOutOfContext | WinEventSkipOwnProcess
        );
    }

    private static void WaitForStop(nint stopEvent)
    {
        nint* handles = stackalloc nint[1];
        handles[0] = stopEvent;

        while (true)
        {
            uint result = Native.MsgWaitForMultipleObjectsEx(
                1,
                handles,
                Infinite,
                QueueStatusAllInput,
                MessageWaitInputAvailable
            );

            if (result == WaitObject0 || result == WaitFailed)
                return;

            if (result != WaitObject0 + 1)
                continue;

            while (Native.PeekMessage(out Message message, 0, 0, 0, PeekMessageRemove))
            {
                if (message.Id == WindowMessageQuit)
                    return;

                Native.TranslateMessage(ref message);
                Native.DispatchMessage(ref message);
            }
        }
    }

    private static void SetAllBorders(uint color)
    {
        Native.EnumWindows(color == DwmColorNone ? &RemoveBorder : &RestoreBorder, 0);
    }

    private static void ScheduleSweeps()
    {
        _sweepTicksRemaining = SweepTicks;
        _sweepTimer = Native.SetTimer(0, _sweepTimer, SweepIntervalMilliseconds, &OnSweepTimer);
    }

    [UnmanagedCallersOnly]
    private static void OnSweepTimer(nint window, uint message, nuint timerId, uint time)
    {
        SetAllBorders(DwmColorNone);

        if (--_sweepTicksRemaining > 0)
            return;

        Native.KillTimer(0, timerId);
        _sweepTimer = 0;
    }

    [UnmanagedCallersOnly]
    private static int RemoveBorder(nint window, nint lParam)
    {
        SetBorder(window, DwmColorNone);
        return 1;
    }

    [UnmanagedCallersOnly]
    private static int RestoreBorder(nint window, nint lParam)
    {
        SetBorder(window, DwmColorDefault);
        return 1;
    }

    [UnmanagedCallersOnly]
    private static void OnWinEvent(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint threadId,
        uint time
    )
    {
        if (window == 0 || objectId != ObjectIdWindow || childId != 0)
            return;

        if (eventType == EventSystemForeground)
        {
            SetAllBorders(DwmColorNone);
            ScheduleSweeps();
            return;
        }

        SetBorder(window, DwmColorNone);
    }

    private static void SetBorder(nint window, uint color)
    {
        Native.DwmSetWindowAttribute(window, DwmWindowAttributeBorderColor, &color, sizeof(uint));
    }
}
