namespace CreatorMicroEditor;
public partial class App : Application
{
    public App() { InitializeComponent(); UserAppTheme = AppTheme.Dark; }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = new MainPage();
        var window = new Window(page) { Title = "Creator Micro Model Keys", Width = 1080, Height = 790, MinimumWidth = 540, MinimumHeight = 580 };
        window.HandlerChanged += (_, _) => {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native) {
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
