namespace WinVrr;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Console.Error.WriteLine("Access denied. Run from an elevated (administrator) terminal.");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        string command = args.Length > 0 && !args[0].StartsWith('-') ? args[0].ToLowerInvariant() : "list";
        return command switch
        {
            "list" => List(args),
            "set" => Set(args),
            "reset" => Reset(args),
            "restart" => Restart(),
            _ => Usage(),
        };
    }

    private static int Usage()
    {
        Console.WriteLine(
            """
            WinVrr - override a monitor's VRR refresh range (EDID override, CRU-style)

              winvrr                                    list monitors and their ranges
              winvrr set <min> [max] [options]          override the vertical refresh range
              winvrr reset [options]                    remove the override, back to factory EDID
              winvrr restart                            restart display adapters (reload EDID)

            Options:
              --monitor <name>    match a monitor by name or hardware id (needed with multiple monitors)
              --no-restart        write the override without restarting the display driver

            Example: winvrr set 80          (keep max, raise the floor to 80 Hz)
            """
        );
        return 1;
    }

    private static int List(string[] args)
    {
        List<MonitorInstance> monitors = Filter(args);
        if (monitors.Count == 0)
        {
            Console.WriteLine("No monitors with EDID data found.");
            return 0;
        }

        foreach (MonitorInstance monitor in monitors)
        {
            Console.WriteLine($@"{monitor.Name}  DISPLAY\{monitor.HardwareId}\{monitor.InstanceId}");
            Console.WriteLine($"  EDID range:     {Format(Edid.GetVerticalRange(monitor.Edid))}");
            if (monitor.Override != null)
                Console.WriteLine($"  Override range: {Format(Edid.GetVerticalRange(monitor.Override))}");
        }

        return 0;
    }

    private static int Set(string[] args)
    {
        List<int> numbers = [];
        for (int i = 1; i < args.Length; i++)
        {
            if (int.TryParse(args[i], out int value))
                numbers.Add(value);
        }

        if (numbers.Count is 0 or > 2)
            return Usage();

        List<MonitorInstance>? targets = SelectTargets(args);
        if (targets == null)
            return 1;

        int changed = 0;
        foreach (MonitorInstance monitor in targets)
        {
            byte[][] blocks = Monitors.SplitBlocks(monitor.Edid);
            (int Min, int Max)? current = Edid.GetVerticalRange(blocks[0]);
            if (current == null)
            {
                Console.Error.WriteLine(
                    $"{monitor.Name} [{monitor.InstanceId}]: no range limits descriptor in EDID, skipped."
                );
                continue;
            }

            int min = numbers[0];
            int max = numbers.Count == 2 ? numbers[1] : current.Value.Max;
            string? rangeError = Edid.SetVerticalRange(blocks[0], min, max);
            if (rangeError != null)
            {
                Console.Error.WriteLine($"{monitor.Name} [{monitor.InstanceId}]: {rangeError}, skipped.");
                continue;
            }

            Monitors.WriteOverride(monitor, blocks);
            Console.WriteLine(
                $"{monitor.Name} [{monitor.InstanceId}]: {current.Value.Min}-{current.Value.Max} Hz -> {min}-{max} Hz"
            );

            if (max < min * 2)
                Console.WriteLine("  Warning: max < 2x min, LFC cannot engage below the floor.");

            changed++;
        }

        if (changed == 0)
            return 1;

        return Apply(args);
    }

    private static int Reset(string[] args)
    {
        List<MonitorInstance>? targets = SelectTargets(args);
        if (targets == null)
            return 1;

        int removed = 0;
        foreach (MonitorInstance monitor in targets)
        {
            if (!Monitors.RemoveOverride(monitor))
                continue;

            Console.WriteLine($"{monitor.Name} [{monitor.InstanceId}]: override removed.");
            removed++;
        }

        if (removed == 0)
        {
            Console.WriteLine("No overrides to remove.");
            return 0;
        }

        return Apply(args);
    }

    private static int Apply(string[] args)
    {
        if (HasFlag(args, "--no-restart"))
        {
            Console.WriteLine("Restart the display driver (winvrr restart) or reboot to apply.");
            return 0;
        }

        return Restart();
    }

    private static int Restart()
    {
        Console.WriteLine("Restarting display adapters (screen will blink)...");
        if (!DeviceRestart.RestartDisplayAdapters())
        {
            Console.Error.WriteLine("Could not restart the display adapter - reboot to apply changes.");
            return 1;
        }

        Console.WriteLine("Done. Verify with the monitor's refresh-rate OSD readout; if unchanged, reboot.");
        return 0;
    }

    private static List<MonitorInstance>? SelectTargets(string[] args)
    {
        List<MonitorInstance> monitors = Filter(args);
        if (monitors.Count == 0)
        {
            Console.Error.WriteLine("No matching monitors found.");
            return null;
        }

        List<string> names = [];
        foreach (MonitorInstance monitor in monitors)
        {
            if (!names.Contains(monitor.Name))
                names.Add(monitor.Name);
        }

        if (names.Count > 1)
        {
            Console.Error.WriteLine($"Multiple monitors found ({string.Join(", ", names)}) - pass --monitor <name>.");
            return null;
        }

        return monitors;
    }

    private static List<MonitorInstance> Filter(string[] args)
    {
        List<MonitorInstance> monitors = Monitors.Enumerate();
        string? filter = GetOption(args, "--monitor");
        if (filter == null)
            return monitors;

        return monitors.FindAll(m =>
            m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || m.HardwareId.Contains(filter, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static string Format((int Min, int Max)? range)
    {
        return range == null ? "none (no range limits descriptor)" : $"{range.Value.Min}-{range.Value.Max} Hz";
    }

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static bool HasFlag(string[] args, string name)
    {
        foreach (string value in args)
        {
            if (string.Equals(value, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
