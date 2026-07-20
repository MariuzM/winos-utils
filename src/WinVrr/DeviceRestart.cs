namespace WinVrr;

internal static unsafe class DeviceRestart
{
    private static readonly Guid DisplayAdapterClass = new("4d36e968-e325-11ce-bfc1-08002be10318");
    private const uint DigcfPresent = 0x2;
    private const uint DifPropertyChange = 0x12;
    private const uint DicsEnable = 1;
    private const uint DicsDisable = 2;
    private const uint DicsFlagConfigSpecific = 2;

    public static bool RestartDisplayAdapters()
    {
        Guid classGuid = DisplayAdapterClass;
        nint devices = Native.SetupDiGetClassDevs(&classGuid, 0, 0, DigcfPresent);
        if (devices == -1)
            return false;

        try
        {
            bool restarted = false;
            SpDevinfoData data = default;
            data.Size = (uint)sizeof(SpDevinfoData);

            for (uint i = 0; Native.SetupDiEnumDeviceInfo(devices, i, &data); i++)
            {
                if (ChangeState(devices, &data, DicsDisable) && ChangeState(devices, &data, DicsEnable))
                    restarted = true;
            }

            return restarted;
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(devices);
        }
    }

    private static bool ChangeState(nint devices, SpDevinfoData* data, uint state)
    {
        SpPropChangeParams change = default;
        change.Header.Size = (uint)sizeof(SpClassInstallHeader);
        change.Header.InstallFunction = DifPropertyChange;
        change.StateChange = state;
        change.Scope = DicsFlagConfigSpecific;

        return Native.SetupDiSetClassInstallParams(devices, data, &change, (uint)sizeof(SpPropChangeParams))
            && Native.SetupDiCallClassInstaller(DifPropertyChange, devices, data);
    }
}
