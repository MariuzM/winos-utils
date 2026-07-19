namespace WindowBorderProtocol;

internal static class Protocol
{
    public const string HelperFileName = "WinBorder.exe";
    public const string MutexName = @"Local\WinUtils.WindowBorders";
    public const string ReadyEventName = @"Local\WinUtils.WindowBorders.Ready";
    public const string StopEventName = @"Local\WinUtils.WindowBorders.Stop";
}
