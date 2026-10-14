using MicroPresets;
using System.Diagnostics;
namespace CreatorMicroEditor;

public partial class MainPage : ContentPage
{
    private readonly EditorDraft draft = new();
    private int selected = 1;
    private bool launchOnStartup = true;
    private bool loading, busy, initialized, connected, panelOpen, transitioning;
    public MainPage()
    {
        InitializeComponent();
        RefreshButton.Loaded += (_, _) => SetHeaderContent(RefreshButton, "\uE72C", "Refresh Models");
        AboutButton.Loaded += (_, _) => SetHeaderContent(AboutButton, "\uE713", "Settings");
        DefaultMode.Loaded += (_, _) => { if (DefaultMode.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Button button) button.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(9, 0, 0, 9); };
        PresetMode.Loaded += (_, _) => { if (PresetMode.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Button button) button.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0, 9, 9, 0); };
        PopupGroup.IsVisible = false;
        Effort.ItemsSource = Presets.EffortNames;
        Keys.Selected += async index => { if (transitioning || busy) return; selected = index; ShowSelection(); await SetPanel(true); };
        SizeChanged += (_, _) => LayoutEditor();
        Header.SizeChanged += (_, _) => LayoutEditor();
        EditorPanel.SizeChanged += (_, _) => LayoutEditor();
        Loaded += async (_, _) => {
            if (initialized) return; initialized = true;
            var args = Environment.GetCommandLineArgs();
            int demo = Array.IndexOf(args, "--capture-demo");
            if (demo >= 0 && demo + 1 < args.Length) {
                draft.Load(new(true, Assignments: Presets.Defaults()));
                SetCatalog(new(true, Models: Presets.KnownModels)); ShowSelection();
                await CaptureDemo(args[demo + 1]); return;
            }
            int preview = Array.IndexOf(args, "--ui-check");
            if (preview >= 0 && preview + 1 < args.Length) {
                draft.Load(new(true, Assignments: Presets.Defaults()));
                SetCatalog(new(true, Models: Presets.KnownModels)); ShowSelection();
                await VerifyUi(args[preview + 1]); return;
            }
            await Run(async () => {
                EditorReply reply;
                try { reply = await EditorProtocol.Request(new("load")); }
                catch (TimeoutException) {
                    var helper = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "CodexMicroPresets.exe"));
                    if (!File.Exists(helper)) throw new IOException("The keyboard helper is missing. Install the complete application.");
                    Process.Start(new ProcessStartInfo(helper, "--listen") { UseShellExecute = false, CreateNoWindow = true });
                    await Task.Delay(1200); reply = await EditorProtocol.Request(new("load"));
                }
                draft.Load(reply); connected = true; SetCatalog(reply); ShowSelection();
                if (draft.RefreshOnOpen) await Refresh();
            }, "");
        };
    }
    private static void SetHeaderContent(Button button, string glyph, string text)
    {
        if (button.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.Button native) return;
        var content = new Microsoft.UI.Xaml.Controls.StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 10 };
        content.Children.Add(new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = glyph, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"), FontSize = 21, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 181, 160, 244)) });
        content.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = text, FontSize = 14, VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center });
        native.Content = content;
    }
    private async Task SetPanel(bool open, bool animate = true)
    {
        if (transitioning || panelOpen == open) return;
        transitioning = true;
        activeDropdown?.Hide();
        var oldKeys = AbsoluteLayout.GetLayoutBounds(Keys);
        var oldInstruction = AbsoluteLayout.GetLayoutBounds(KeyInstruction);
        var oldPanel = AbsoluteLayout.GetLayoutBounds(PopupGroup);
        double oldHeight = Body.HeightRequest;
        panelOpen = open;
        PopupGroup.Opacity = open ? 0 : 1;
        PopupGroup.IsVisible = true;
        LayoutEditor(true);
        var newKeys = AbsoluteLayout.GetLayoutBounds(Keys);
        // Keep the existing layout stationary during motion. Only transforms animate;
        // committing the destination layout happens after the final frame.
        AbsoluteLayout.SetLayoutBounds(Keys, oldKeys);
        AbsoluteLayout.SetLayoutBounds(KeyInstruction, oldInstruction);
        if (!open) {
            AbsoluteLayout.SetLayoutBounds(PopupGroup, oldPanel);
        }
        Body.HeightRequest = Math.Max(oldHeight, Body.HeightRequest);
        Pointer.IsVisible = true;
        double dx = newKeys.X - oldKeys.X, dy = newKeys.Y - oldKeys.Y;
        var completion = new TaskCompletionSource();
        Action<double> frame = t => {
            // Opening: move for 360ms, then reveal for 180ms. Closing reverses it.
            double move = Easing.SinInOut.Ease(Math.Clamp(open ? t * 1.5 : (t - 1.0 / 3) * 1.5, 0, 1));
            double fade = Easing.SinInOut.Ease(Math.Clamp(open ? (t - 2.0 / 3) * 3 : t * 3, 0, 1));
            Keys.TranslationX = KeyInstruction.TranslationX = dx * move;
            Keys.TranslationY = KeyInstruction.TranslationY = dy * move;
            PopupGroup.Opacity = open ? fade : 1 - fade;
        };
        frame(0);
        uint duration = animate && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled ? 540u : 1u;
        new Animation(frame).Commit(this, "EditorTransition", 16, duration, Easing.Linear, (_, _) => completion.TrySetResult());
        await completion.Task;
        PopupGroup.IsVisible = open;
        transitioning = false;
        LayoutEditor();
        PopupGroup.Opacity = 1;
        PopupGroup.TranslationX = 0;
        Keys.TranslationX = Keys.TranslationY = KeyInstruction.TranslationX = KeyInstruction.TranslationY = 0;
        Keys.Update(draft.Assignments, open ? selected : -1);
    }
    private void LayoutEditor(bool duringTransition = false)
    {
        if (Width <= 0 || (transitioning && !duringTransition)) return;
        const double margin = 20, gap = 24;
        double available = Math.Max(1, Width - margin * 2);
        double board = Math.Min(350, available);
        double panel = board;
        double bx = (!panelOpen) ? (Width - board) / 2 : (Width - board - gap - panel) / 2;
        double px = bx + board + gap;
        double panelHeight = board;
        double viewport = Math.Max(0, Height - Header.Height - Footer.Height);
        double groupTop = -42;
        double groupBottom = board;
        double by = Math.Max(24, (viewport - (groupBottom - groupTop)) / 2) - groupTop;
        AbsoluteLayout.SetLayoutBounds(KeyInstruction, new Rect(bx, by - 42, board, 30));
        double py = by;
        Keys.MaximumWidthRequest = board; Keys.HeightRequest = board;
        AbsoluteLayout.SetLayoutBounds(Keys, new Rect(bx, by, board, board));
        AbsoluteLayout.SetLayoutBounds(PopupGroup, new Rect(px - 19, py, panel + 19, panelHeight));
        AbsoluteLayout.SetLayoutBounds(EditorPanel, new Rect(19, 0, panel, panelHeight));
        AbsoluteLayout.SetLayoutBounds(Pointer, new Rect(0, panelHeight / 2 - 18, 22, 36));
        Pointer.IsVisible = panelOpen;
        Body.HeightRequest = Math.Max(viewport, (panelOpen ? Math.Max(by + board, py + panelHeight) : by + board) + 24);        Header.Padding = new Thickness(20, 12);
        Grid.SetRow(RefreshButton, 0); Grid.SetRow(AboutButton, 0);
        Grid.SetColumnSpan(BrandText, 1);
        AppTitle.Text = "Creator Micro";
        AppTitle.FontSize = 24;
        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window native) {
            double scale = native.Content.XamlRoot?.RasterizationScale ?? 1;
            native.AppWindow.TitleBar.SetDragRectangles([new Windows.Graphics.RectInt32(0, 0, (int)(Math.Max(80, RefreshButton.X - 8) * scale), (int)(64 * scale))]);
        }
    }
    private Microsoft.UI.Xaml.Controls.MenuFlyout? activeDropdown;
    private void ShowDropdown(object? sender, IEnumerable<string> choices, string current, Action<string> select)
    {
        if (busy || sender is not Button { Handler.PlatformView: Microsoft.UI.Xaml.FrameworkElement anchor }) return;
        activeDropdown?.Hide();
        var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft };
        foreach (string value in choices) {
            var item = new Microsoft.UI.Xaml.Controls.ToggleMenuFlyoutItem { Text = value, IsChecked = value == current, MinWidth = anchor.ActualWidth };
            item.Click += (_, _) => select(value);
            menu.Items.Add(item);
        }
        activeDropdown = menu;
        menu.ShowAt(anchor);
    }
    private void ChooseModel(object? sender, EventArgs e) => ShowDropdown(sender, Model.ItemsSource.Cast<string>(), draft.Assignments[selected].Model, value => Model.SelectedItem = value);
    private void ChooseEffort(object? sender, EventArgs e) => ShowDropdown(sender, Presets.EffortNames.Reverse(), draft.Assignments[selected].Effort, value => Effort.SelectedIndex = Array.IndexOf(Presets.EffortNames, value));
    private void SetCatalog(EditorReply reply)
    {
        loading = true;
        launchOnStartup = reply.LaunchOnStartup;
        Model.ItemsSource = (reply.Models ?? Presets.KnownModels).Concat(draft.Assignments.Select(p => p.Model)).Distinct().ToList();
        AutoRefresh.IsChecked = draft.RefreshOnOpen;
        CatalogStatus.Text = reply.CheckedAt is {} date ? $"Checked {date.ToLocalTime():MMM d, h:mm tt}" : "Starter list - refresh from Codex";
        loading = false;
    }
    private void ShowSelection()
    {
        loading = true;
        var p = draft.Assignments[selected];
        SelectionTitle.Text = p.Enabled ? p.Model.Split(' ').Last() : "Default"; ModelValue.Text = p.Model; EffortValue.Text = p.Effort;
        DefaultMode.BackgroundColor = Color.FromArgb(p.Enabled ? "#292A31" : "#7566DC"); PresetMode.BackgroundColor = Color.FromArgb(p.Enabled ? "#7566DC" : "#292A31"); Override.IsToggled = p.Enabled; PresetFields.IsVisible = p.Enabled;
        Model.SelectedItem = p.Model;
        Effort.SelectedIndex = p.EffortPosition - 1;
        DefaultHint.Text = p.Enabled ? "" : "No model override"; DefaultHint.IsVisible = !p.Enabled;
        Keys.Update(draft.Assignments, panelOpen ? selected : -1); loading = false;
    }
    private void Edit(Func<Preset, Preset> edit)
    {
        if (loading || busy) return;
        draft.Assignments[selected] = edit(draft.Assignments[selected]);
        ShowSelection(); Status.Text = draft.IsDirty ? "Unsaved changes" : "No unsaved changes";
    }
    private Microsoft.UI.Xaml.Controls.ContentDialog? aboutDialog;
    private async void AboutClicked(object? sender, EventArgs e)
    {
        if (aboutDialog != null) return;
        aboutDialog = AboutDialog.Create(((Microsoft.UI.Xaml.FrameworkElement)Handler!.PlatformView!).XamlRoot, CatalogStatus.Text, draft.RefreshOnOpen, launchOnStartup, !busy, async (refresh, startup) => {
            var reply = await EditorProtocol.Request(new("preferences", RefreshOnOpen: refresh, LaunchOnStartup: startup));
            draft.SetRefreshPreference(reply.RefreshOnOpen); SetCatalog(reply);
        });
        try { await aboutDialog.ShowAsync(); } finally { aboutDialog = null; }
    }
    private void DefaultClicked(object? sender, EventArgs e) => Edit(p => p with { Enabled = false });
    private void PresetClicked(object? sender, EventArgs e) => Edit(p => p with { Enabled = true });
    private void OverrideChanged(object? sender, ToggledEventArgs e) => Edit(p => p with { Enabled = e.Value });
    private void EffortChanged(object? sender, EventArgs e) { if (Effort.SelectedIndex >= 0) Edit(p => p with { EffortPosition = Effort.SelectedIndex + 1, Effort = Presets.EffortNames[Effort.SelectedIndex] }); }
    private void ModelChanged(object? sender, EventArgs e)
    {
        if (loading) return;
        if (Model.SelectedItem is string value) Edit(p => p with { Model = value });
    }
    private void AutoChanged(object? sender, CheckedChangedEventArgs e) { if (!loading) { draft.RefreshOnOpen = e.Value; Status.Text = "Unsaved changes"; } }
    private async Task Run(Func<Task> action, string success, bool saving = false)
    {
        if (busy) return;
        busy = true; Body.IsEnabled = Save.IsEnabled = Cancel.IsEnabled = false; Status.Text = "Working...";
        SetSaving(saving);
        try { await action(); Status.Text = success; }
        catch (Exception e) { Status.Text = e.Message; }
        finally { SetSaving(false); busy = false; Body.IsEnabled = Save.IsEnabled = connected; Cancel.IsEnabled = true; }
    }
    private void SetSaving(bool saving)
    {
        Save.Text = saving ? "" : "Done";
        SaveSpinner.IsVisible = SaveSpinner.IsRunning = saving;
        SemanticProperties.SetDescription(Save, saving ? "Saving assignments" : "Save assignments");
    }
    private async Task Refresh() { var reply = await EditorProtocol.Request(new("refresh")); SetCatalog(reply); ShowSelection(); }
    private async void RefreshClicked(object? sender, EventArgs e) => await Run(Refresh, "Model list updated. Assignments unchanged.");
    private async void SaveClicked(object? sender, EventArgs e) => await Run(async () => {
        var reply = await EditorProtocol.Request(draft.SaveRequest()); draft.Load(reply); SetCatalog(reply); ShowSelection(); await SetPanel(false);
    }, "Saved. Your keys are ready in Codex.", saving: true);
    public async Task<bool> CanClose() => !busy && (!draft.IsDirty || await DisplayAlertAsync("Unsaved changes", "Discard your unsaved changes?", "Discard", "Keep editing"));
    private async void CancelClicked(object? sender, EventArgs e) { if (transitioning || busy) return; draft.Discard(); loading = true; AutoRefresh.IsChecked = draft.RefreshOnOpen; loading = false; ShowSelection(); Status.Text = "Changes discarded"; await SetPanel(false); }
    private async void WebsiteClicked(object? sender, EventArgs e) => await OpenLink("https://www.LanceLarsen.com");
    private async void GithubClicked(object? sender, EventArgs e) => await OpenLink("https://github.com/lancelarsen/Creator-Micro-Model-Key-Mapper-for-Codex");
    private async Task OpenLink(string url) { try { await Launcher.Default.OpenAsync(url); } catch (Exception e) { Status.Text = e.Message; } }
}
