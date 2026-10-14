namespace CreatorMicroEditor;
public partial class App : Application
{
    public App() { InitializeComponent(); UserAppTheme = AppTheme.Dark; }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = new MainPage();
        var window = new Window(page) { Title = "Creator Micro - Model Key Mapper for Codex", Width = 880, Height = 670, MinimumWidth = 880, MinimumHeight = 670 };
        window.HandlerChanged += (_, _) => {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native) {
                native.ExtendsContentIntoTitleBar = true;
                native.AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                native.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                native.AppWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                native.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app.ico"));
                bool approved = false;
                native.AppWindow.Closing += async (_, e) => {
                    if (approved) return;
                    e.Cancel = true;
                    if (await page.CanClose()) { approved = true; native.Close(); }
                };
            }
        };
        return window;
    }
}
