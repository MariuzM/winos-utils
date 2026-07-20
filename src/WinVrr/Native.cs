using System.Runtime.InteropServices;

namespace WinVrr;

internal static unsafe partial class Native
{
    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", SetLastError = true)]
    public static partial nint SetupDiGetClassDevs(Guid* classGuid, nint enumerator, nint parent, uint flags);

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiEnumDeviceInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiEnumDeviceInfo(nint devices, uint index, SpDevinfoData* data);

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiSetClassInstallParamsW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiSetClassInstallParams(
        nint devices,
        SpDevinfoData* data,
        SpPropChangeParams* installParams,
        uint size
    );

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiCallClassInstaller", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiCallClassInstaller(uint installFunction, nint devices, SpDevinfoData* data);

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiDestroyDeviceInfoList")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiDestroyDeviceInfoList(nint devices);
}

[StructLayout(LayoutKind.Sequential)]
internal struct SpDevinfoData
{
    public uint Size;
    public Guid ClassGuid;
    public uint DevInst;
    public nint Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SpClassInstallHeader
{
    public uint Size;
    public uint InstallFunction;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SpPropChangeParams
{
    public SpClassInstallHeader Header;
    public uint StateChange;
    public uint Scope;
    public uint HwProfile;
}
