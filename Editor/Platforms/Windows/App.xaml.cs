using System.Diagnostics;
using System.Runtime.InteropServices;
namespace CreatorMicroEditor.WinUI;

public partial class App : MauiWinUIApplication
{
    private readonly Mutex? instance;
    public App()
    {
        if (!Environment.GetCommandLineArgs().Any(arg => arg is "--ui-check" or "--capture-demo")) {
            instance = new Mutex(true, "Local\\CreatorMicroModelKeys.Editor", out bool created);
            if (!created) {
                foreach (var process in Process.GetProcessesByName("CreatorMicroEditor")) {
                    if (process.Id != Environment.ProcessId && process.SessionId == Process.GetCurrentProcess().SessionId && process.MainWindowHandle != 0) {
                        ShowWindow(process.MainWindowHandle, 9); SetForegroundWindow(process.MainWindowHandle); break;
                    }
                }
                Environment.Exit(0);
            }
        }
        InitializeComponent();
    }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
}
