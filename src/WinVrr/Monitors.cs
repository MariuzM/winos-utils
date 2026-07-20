using Microsoft.Win32;

namespace WinVrr;

internal sealed class MonitorInstance
{
    public required string Name { get; init; }
    public required string HardwareId { get; init; }
    public required string InstanceId { get; init; }
    public required string ParametersPath { get; init; }
    public required byte[] Edid { get; init; }
    public byte[]? Override { get; init; }
}

internal static class Monitors
{
    private const string DisplayRoot = @"SYSTEM\CurrentControlSet\Enum\DISPLAY";
    private const string OverrideKeyName = "EDID_OVERRIDE";

    public static List<MonitorInstance> Enumerate()
    {
        List<MonitorInstance> result = [];
        using RegistryKey? root = Registry.LocalMachine.OpenSubKey(DisplayRoot);
        if (root == null)
            return result;

        foreach (string hardwareId in root.GetSubKeyNames())
        {
            using RegistryKey? device = root.OpenSubKey(hardwareId);
            if (device == null)
                continue;

            foreach (string instanceId in device.GetSubKeyNames())
            {
                using RegistryKey? parameters = device.OpenSubKey($"{instanceId}\\Device Parameters");
                if (parameters?.GetValue("EDID") is not byte[] edid || !Edid.HasValidHeader(edid))
                    continue;

                byte[]? overrideBlock = null;
                using RegistryKey? overrideKey = parameters.OpenSubKey(OverrideKeyName);
                if (overrideKey?.GetValue("0") is byte[] block && block.Length >= Edid.BlockSize)
                    overrideBlock = block;

                result.Add(
                    new MonitorInstance
                    {
                        Name = Edid.GetName(edid) ?? hardwareId,
                        HardwareId = hardwareId,
                        InstanceId = instanceId,
                        ParametersPath = $@"{DisplayRoot}\{hardwareId}\{instanceId}\Device Parameters",
                        Edid = edid,
                        Override = overrideBlock,
                    }
                );
            }
        }

        return result;
    }

    public static byte[][] SplitBlocks(byte[] edid)
    {
        int count = Math.Max(1, edid.Length / Edid.BlockSize);
        byte[][] blocks = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            blocks[i] = new byte[Edid.BlockSize];
            Array.Copy(edid, i * Edid.BlockSize, blocks[i], 0, Edid.BlockSize);
        }

        return blocks;
    }

    public static void WriteOverride(MonitorInstance monitor, byte[][] blocks)
    {
        using RegistryKey parameters =
            Registry.LocalMachine.OpenSubKey(monitor.ParametersPath, true)
            ?? throw new UnauthorizedAccessException(monitor.ParametersPath);

        parameters.DeleteSubKeyTree(OverrideKeyName, false);
        using RegistryKey overrideKey = parameters.CreateSubKey(OverrideKeyName);
        for (int i = 0; i < blocks.Length; i++)
            overrideKey.SetValue(i.ToString(), blocks[i], RegistryValueKind.Binary);
    }

    public static bool RemoveOverride(MonitorInstance monitor)
    {
        using RegistryKey? parameters = Registry.LocalMachine.OpenSubKey(monitor.ParametersPath, true);
        if (parameters == null)
            return false;

        bool exists;
        using (RegistryKey? overrideKey = parameters.OpenSubKey(OverrideKeyName))
            exists = overrideKey != null;

        if (!exists)
            return false;

        parameters.DeleteSubKeyTree(OverrideKeyName);
        return true;
    }
}
