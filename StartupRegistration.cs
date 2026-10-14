using System.IO;
using System.Runtime.InteropServices;

namespace MicroPresets;

internal static class StartupRegistration
{
    private static string Shortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Creator Micro - Model Key Mapper for Codex.lnk");
    internal static bool Enabled => File.Exists(Shortcut);
    internal static void SetEnabled(bool enabled)
    {
        if (!enabled) { File.Delete(Shortcut); return; }
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows startup shortcuts are unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic? shortcut = null;
        try {
            shortcut = shell.CreateShortcut(Shortcut);
            shortcut.TargetPath = Environment.ProcessPath!;
            shortcut.Arguments = "--listen";
            shortcut.WorkingDirectory = AppContext.BaseDirectory;
            shortcut.WindowStyle = 7;
            shortcut.Save();
        }
        finally {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
