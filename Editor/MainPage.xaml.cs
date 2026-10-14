using MicroPresets;
using System.Diagnostics;
namespace CreatorMicroEditor;

public partial class MainPage : ContentPage
{
    private readonly EditorDraft draft = new();
    private int selected = 1;
    private bool loading, busy, initialized, connected;
    public MainPage()
    {
        InitializeComponent();
        Effort.ItemsSource = Presets.EffortNames;
        Keys.Selected += index => { selected = index; ShowSelection(); };
        SizeChanged += (_, _) => {
            bool narrow = Width < 880;
            Body.ColumnDefinitions[0].Width = new GridLength(narrow ? 1 : 1.15, GridUnitType.Star);
            Body.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : GridLength.Star;
            Grid.SetColumn(EditorPanel, narrow ? 0 : 1); Grid.SetRow(EditorPanel, narrow ? 1 : 0);
        };
        Loaded += async (_, _) => {
            if (initialized) return; initialized = true;
            var args = Environment.GetCommandLineArgs();
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
            }, "Ready. Select a key to edit.");
        };
    }
    private void SetCatalog(EditorReply reply)
    {
        loading = true;
        Model.ItemsSource = (reply.Models ?? Presets.KnownModels).Concat(draft.Assignments.Select(p => p.Model)).Distinct().Append("Other model...").ToList();
        AutoRefresh.IsChecked = draft.RefreshOnOpen;
        CatalogStatus.Text = reply.CheckedAt is {} date ? $"Checked {date.ToLocalTime():MMM d, h:mm tt}" : "Starter list - refresh from Codex";
        loading = false;
    }
    private void ShowSelection()
    {
        loading = true;
        var p = draft.Assignments[selected];
        SelectionTitle.Text = p.Enabled ? p.Model : Presets.DefaultActions[selected];
        Override.IsToggled = p.Enabled; PresetFields.IsVisible = p.Enabled;
        Model.SelectedItem = Model.ItemsSource?.Contains(p.Model) == true ? p.Model : "Other model...";
        CustomModel.Text = p.Model; CustomModel.IsVisible = Equals(Model.SelectedItem, "Other model...");
        Effort.SelectedIndex = p.EffortPosition - 1;
        DefaultHint.Text = p.Enabled ? "This key switches the model and reasoning in Codex." : $"Keeps {Presets.DefaultActions[selected].ToLowerInvariant()} (default).";
        Keys.Update(draft.Assignments, selected); loading = false;
    }
    private void Edit(Func<Preset, Preset> edit)
    {
        if (loading || busy) return;
        draft.Assignments[selected] = edit(draft.Assignments[selected]);
        ShowSelection(); Status.Text = draft.IsDirty ? "Unsaved changes" : "No unsaved changes";
    }
    private void OverrideChanged(object? sender, ToggledEventArgs e) => Edit(p => p with { Enabled = e.Value });
    private void EffortChanged(object? sender, EventArgs e) { if (Effort.SelectedIndex >= 0) Edit(p => p with { EffortPosition = Effort.SelectedIndex + 1, Effort = Presets.EffortNames[Effort.SelectedIndex] }); }
    private void ModelChanged(object? sender, EventArgs e)
    {
        if (loading) return;
        if (Model.SelectedItem is string value && value != "Other model...") Edit(p => p with { Model = value });
        else CustomModel.IsVisible = true;
    }
    private void CustomChanged(object? sender, TextChangedEventArgs e)
    {
        if (loading || busy) return;
        draft.Assignments[selected] = draft.Assignments[selected] with { Model = e.NewTextValue.Trim() };
        Keys.Update(draft.Assignments, selected); Status.Text = "Unsaved changes";
    }
    private void AutoChanged(object? sender, CheckedChangedEventArgs e) { if (!loading) { draft.RefreshOnOpen = e.Value; Status.Text = "Unsaved changes"; } }
    private async Task Run(Func<Task> action, string success)
    {
        if (busy) return;
        busy = true; Body.IsEnabled = Save.IsEnabled = Cancel.IsEnabled = false; Status.Text = "Working...";
        try { await action(); Status.Text = success; }
        catch (Exception e) { Status.Text = e.Message + " Try again, or reopen settings."; }
        finally { busy = false; Body.IsEnabled = Save.IsEnabled = connected; Cancel.IsEnabled = true; }
    }
    private async Task Refresh() { var reply = await EditorProtocol.Request(new("refresh")); SetCatalog(reply); ShowSelection(); }
    private async void RefreshClicked(object? sender, EventArgs e) => await Run(Refresh, "Model list updated. Assignments unchanged.");
    private async void SaveClicked(object? sender, EventArgs e) => await Run(async () => {
        var reply = await EditorProtocol.Request(draft.SaveRequest()); draft.Load(reply); SetCatalog(reply); ShowSelection();
    }, "Saved. Your keys are ready in Codex.");
    public async Task<bool> CanClose() => !busy && (!draft.IsDirty || await DisplayAlertAsync("Unsaved changes", "Discard your unsaved changes?", "Discard", "Keep editing"));
    private void CancelClicked(object? sender, EventArgs e) { if (Window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native) native.Close(); }
    private async void WebsiteClicked(object? sender, EventArgs e) => await OpenLink("https://www.LanceLarsen.com");
    private async void GithubClicked(object? sender, EventArgs e) => await OpenLink("https://github.com/lancelarsen/micro-model-presets");
    private async Task OpenLink(string url) { try { await Launcher.Default.OpenAsync(url); } catch (Exception e) { Status.Text = e.Message; } }
}
