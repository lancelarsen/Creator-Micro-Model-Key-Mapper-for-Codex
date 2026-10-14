using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace MicroPresets;

internal static class PresetRuntime
{
    private const string MutexName = "Local\\CodexMicroPresets.Instance";
    private const string StopName = "Local\\CodexMicroPresets.Stop";

    internal static void Stop()
    {
        try { using var stop = EventWaitHandle.OpenExisting(StopName); stop.Set(); }
        catch (WaitHandleCannotBeOpenedException) { return; }
        // Restore must not overlap a selector that is still shutting down.
        try
        {
            using var mutex = Mutex.OpenExisting(MutexName);
            bool acquired;
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new TimeoutException("The preset helper is still stopping. Try again in a moment.");
            mutex.ReleaseMutex();
        }
        catch (WaitHandleCannotBeOpenedException) { }
    }

    internal static void Run(string logPath, bool showSettings = false)
    {
        using var mutex = new Mutex(true, MutexName, out bool created);
        if (!created) {
            if (showSettings) { try { using var show = EventWaitHandle.OpenExisting("Local\\CreatorMicroModelKeys.ShowEditor"); show.Set(); } catch (WaitHandleCannotBeOpenedException) { } }
            return;
        }
        using var stop = new EventWaitHandle(false, EventResetMode.AutoReset, StopName);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logPath))!);
        if (File.Exists(logPath) && new FileInfo(logPath).Length > 1_048_576) File.WriteAllText(logPath, "");
        using var writer = new StreamWriter(logPath, append: true) { AutoFlush = true };
        object logLock = new();
        void Log(object data) { lock (logLock) writer.WriteLine(JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, data })); }
        using var menu = new ContextMenuStrip();
        using var icon = new NotifyIcon { Icon = IconArtwork.Load(), Text = "Creator Micro - Model Key Mapper for Codex", ContextMenuStrip = menu, Visible = true };
        bool paused = false, exiting = false, working = false;
        Preset? pending = null;
        Process? worker = null;
        var decoder = new ReportDecoder();
        Process? settingsWindow = null;
        bool editorBusy = false, stopRequested = false;
        var assignmentItems = new List<ToolStripItem>();
        long lastPress = 0;
        string? lastPosition = null;

        void Notify(string message)
        {
            icon.BalloonTipTitle = "Creator Micro - Model Key Mapper for Codex";
            icon.BalloonTipText = message;
            icon.ShowBalloonTip(2500);
        }

        var dispatcher = new Control();
        _ = dispatcher.Handle;
        async void SelectNext(Preset preset)
        {
            if (exiting || paused || editorBusy) return;
            if (working) { pending = preset; return; }
            working = true;
            try
            {
                nint window = ModelSelector.FindWindow();
                if (!ModelSelector.IsForeground(window)) throw new InvalidOperationException("Focus Codex before pressing a model key.");
                string executable = Path.Combine(AppContext.BaseDirectory, "CodexMicroPresets.exe");
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("--select"); start.ArgumentList.Add(preset.Position);
                worker = Process.Start(start) ?? throw new InvalidOperationException("The preset selector could not start.");
                Task<string> output = worker.StandardOutput.ReadToEndAsync(), errors = worker.StandardError.ReadToEndAsync();
                try { await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
                catch (TimeoutException) { worker.Kill(entireProcessTree: true); await worker.WaitForExitAsync(); throw new TimeoutException("Preset selection timed out."); }
                string result = await output, error = await errors;
                if (worker.ExitCode != 0) throw new InvalidOperationException(error.Trim());
                Log(new { kind = "preset-selected", preset.Position, preset.Model, preset.Effort, result = result.Trim() });
                string tooltip = $"{preset.Model} {preset.Effort}";
                icon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
            }
            catch (Exception error)
            {
                if (!exiting) { Log(new { kind = "selection-error", preset.Position, message = error.Message }); Notify(error.Message); }
            }
            finally
            {
                worker?.Dispose(); worker = null; working = false;
                var next = pending; pending = null;
                if (next != null && !exiting) SelectNext(next);
            }
        }

        menu.Items.Add("Configure model assignments…", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        void UpdateAssignmentMenu()
        {
            foreach (var item in assignmentItems) menu.Items.Remove(item);
            assignmentItems.Clear();
            foreach (var preset in Presets.All)
            {
                var item = menu.Items.Add(preset.Enabled ? $"{preset.Model} — {preset.Effort}" : $"{Presets.DefaultActions[Array.IndexOf(Presets.Positions, preset.Position)]} (default)");
                item.Enabled = false;
                assignmentItems.Add(item);
            }
        }
        void ShowSettings()
        {
            if (working) { Notify("Wait for model selection to finish, then open settings."); return; }
            if (settingsWindow is { HasExited: false }) { ShowWindow(settingsWindow.MainWindowHandle, 9); SetForegroundWindow(settingsWindow.MainWindowHandle); return; }
            try
            {
                settingsWindow?.Dispose();
                var path = Path.Combine(AppContext.BaseDirectory, "editor", "CreatorMicroEditor.exe");
                if (!File.Exists(path)) throw new FileNotFoundException("The MAUI editor is missing. Reinstall the complete application.");
                settingsWindow = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception error) { Notify(error.Message); }
        }
        using var server = new EditorServer(request => {
            var completion = new TaskCompletionSource<EditorReply>(TaskCreationOptions.RunContinuationsAsynchronously);
            dispatcher.BeginInvoke(async () => {
                if (working || editorBusy) { completion.SetResult(new(false, "A key operation is in progress. Try again in a moment.")); return; }
                editorBusy = true;
                try {
                    var catalog = ModelCatalog.Load();
                    switch (request.Operation) {
                        case "load": break;
                        case "preferences":
                            StartupRegistration.SetEnabled(request.LaunchOnStartup);
                            catalog = catalog with { RefreshOnOpen = request.RefreshOnOpen }; catalog.Save(); break;
                        case "refresh":
                            try {
                                catalog = catalog.WithRefresh(await StaWork.Run(ModelSelector.ListAvailableModels));
                                catalog.Save();
                            }
                            finally {
                                // Return to the mapper only if Codex still owns focus;
                                // respect a user who switched to another app while refreshing.
                                if (settingsWindow is { HasExited: false }) {
                                    try {
                                        if (ModelSelector.IsForeground(ModelSelector.FindWindow())) {
                                            settingsWindow.Refresh();
                                            SetForegroundWindow(settingsWindow.MainWindowHandle);
                                        }
                                    } catch (InvalidOperationException) { }
                                }
                            }
                            break;
                        case "save":
                            var before = Presets.WithAssignments(request.Original ?? []);
                            var after = Presets.WithAssignments(request.Assignments ?? []);
                            if (!PresetStore.Load().SequenceEqual(before)) throw new InvalidOperationException("Assignments changed. Close and reopen settings before saving.");
                            await StaWork.Run(() => { MicroBindings.SaveAssignments(before, after); return true; });
                            Presets.SetAll(after); UpdateAssignmentMenu();
                            catalog = catalog with { RefreshOnOpen = request.RefreshOnOpen }; catalog.Save(); break;
                        default: throw new InvalidOperationException("Unsupported settings request.");
                    }
                    completion.SetResult(new(true, Assignments: PresetStore.Load(), Models: catalog.Models, CheckedAt: catalog.CheckedAt, RefreshOnOpen: catalog.RefreshOnOpen, LaunchOnStartup: StartupRegistration.Enabled));
                } catch (Exception e) { completion.SetResult(new(false, e.Message)); }
                finally { editorBusy = false; }
            });
            return completion.Task;
        });
        UpdateAssignmentMenu();
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\CreatorMicroModelKeys.ShowEditor");
        var showRegistration = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => dispatcher.BeginInvoke(ShowSettings), null, Timeout.Infinite, false);
        DateTime lastSettingsWrite = File.Exists(PresetStore.SettingsPath) ? File.GetLastWriteTimeUtc(PresetStore.SettingsPath) : DateTime.MinValue;
        using var settingsTimer = new System.Windows.Forms.Timer { Interval = 1000, Enabled = true };
        settingsTimer.Tick += (_, _) => {
            if (stopRequested && !working && !editorBusy) { System.Windows.Forms.Application.ExitThread(); return; }
            if (working || editorBusy) return;
            var modified = File.Exists(PresetStore.SettingsPath) ? File.GetLastWriteTimeUtc(PresetStore.SettingsPath) : DateTime.MinValue;
            if (modified == lastSettingsWrite) return;
            try { Presets.SetAll(PresetStore.Load()); UpdateAssignmentMenu(); lastSettingsWrite = modified; }
            catch (Exception e) { Log(new { kind = "settings-reload-error", message = e.Message }); lastSettingsWrite = modified; }
        };
        var pause = menu.Items.Add("Pause");
        pause.Click += (_, _) => { paused = !paused; pause.Text = paused ? "Resume" : "Pause"; pending = null; icon.Text = paused ? "Creator Micro - Model Key Mapper for Codex: paused" : "Creator Micro - Model Key Mapper for Codex"; };
        menu.Items.Add("Exit", null, (_, _) => {
            if (editorBusy) { Notify("Wait for the settings operation to finish before exiting."); return; }
            System.Windows.Forms.Application.ExitThread();
        });
        icon.MouseClick += (_, eventArgs) => { if (eventArgs.Button == MouseButtons.Left) ShowSettings(); };
        using var listener = new MicroInputListener((device, report) => {
            if (paused || exiting || editorBusy) return;
            foreach (var preset in decoder.Feed(device, report))
            {
                long now = Environment.TickCount64;
                if (lastPosition == preset.Position && now - lastPress < 300) continue;
                lastPosition = preset.Position; lastPress = now;
                Log(new { kind = "physical-press", preset.Position, preset.HardwareKey });
                dispatcher.BeginInvoke(() => SelectNext(preset));
            }
        }, Log);
        var registration = ThreadPool.RegisterWaitForSingleObject(stop, (_, _) => dispatcher.BeginInvoke(() => {
            stopRequested = true;
            if (!working && !editorBusy) System.Windows.Forms.Application.ExitThread();
        }), null, Timeout.Infinite, executeOnlyOnce: true);
        Log(new { kind = "running", pid = Environment.ProcessId, positions = Presets.All.Where(p => p.Enabled).Select(p => p.Position).ToArray() });
        if (showSettings) dispatcher.BeginInvoke(ShowSettings);
        try { System.Windows.Forms.Application.Run(); }
        finally
        {
            exiting = true;
            registration.Unregister(null);
            showRegistration.Unregister(null);
            pending = null;
            if (worker != null && !worker.HasExited) { worker.Kill(entireProcessTree: true); worker.WaitForExit(2000); }
            icon.Visible = false;
            dispatcher.Dispose();
            Log(new { kind = "stopped" });
            mutex.ReleaseMutex();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);

}
