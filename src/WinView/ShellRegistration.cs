using Microsoft.Win32;

namespace WinView;

// Registers WinView under HKCU\Software\Classes\Applications so it shows up in Explorer's
// "Open with" list. Deliberately does NOT claim any file type as the default - Windows 11 treats
// programmatic default-app changes as user-hostile and will either prompt or ignore them, and
// silently hijacking .png is not something a viewer should do on first run.
internal static class ShellRegistration
{
    private static readonly string[] Extensions =
    [
        ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".tif", ".tiff", ".ico",
    ];

    private static string KeyPath(string exeName) => $@"Software\Classes\Applications\{exeName}";

    public static string Register()
    {
        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the running executable path.");

        string exeName = Path.GetFileName(exePath);
        string root = KeyPath(exeName);

        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(root))
        {
            key.SetValue("FriendlyAppName", "WinView", RegistryValueKind.String);
        }

        using (RegistryKey command = Registry.CurrentUser.CreateSubKey($@"{root}\shell\open\command"))
        {
            command.SetValue(null, $"\"{exePath}\" \"%1\"", RegistryValueKind.String);
        }

        // SupportedTypes is what keeps WinView out of the "Open with" list for file types it cannot
        // decode - without it Explorer offers it for everything.
        using (RegistryKey types = Registry.CurrentUser.CreateSubKey($@"{root}\SupportedTypes"))
        {
            foreach (string extension in Extensions)
                types.SetValue(extension, string.Empty, RegistryValueKind.String);
        }

        return exePath;
    }

    public static void Unregister()
    {
        string exePath = Environment.ProcessPath ?? string.Empty;
        string exeName = Path.GetFileName(exePath);
        if (string.IsNullOrEmpty(exeName))
            return;

        Registry.CurrentUser.DeleteSubKeyTree(KeyPath(exeName), throwOnMissingSubKey: false);
    }
}
