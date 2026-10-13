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

    internal static void Run(string logPath)
    {
        using var mutex = new Mutex(true, MutexName, out bool created);
        if (!created) return;
        using var stop = new EventWaitHandle(false, EventResetMode.AutoReset, StopName);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logPath))!);
        if (File.Exists(logPath) && new FileInfo(logPath).Length > 1_048_576) File.WriteAllText(logPath, "");
        using var writer = new StreamWriter(logPath, append: true) { AutoFlush = true };
        object logLock = new();
        void Log(object data) { lock (logLock) writer.WriteLine(JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, data })); }
        using var menu = new ContextMenuStrip();
        using var icon = new NotifyIcon { Icon = IconArtwork.Load(), Text = "Micro model presets", ContextMenuStrip = menu, Visible = true };
        bool paused = false, exiting = false, working = false;
        Preset? pending = null;
        Process? worker = null;
        var decoder = new ReportDecoder();
        Form? settingsWindow = null;
        var assignmentItems = new List<ToolStripItem>();
        long lastPress = 0;
        string? lastPosition = null;
        uint threadId = GetCurrentThreadId();

        void Notify(string message)
        {
            icon.BalloonTipTitle = "Codex Micro presets";
            icon.BalloonTipText = message;
            icon.ShowBalloonTip(2500);
        }

        var dispatcher = new Control();
        _ = dispatcher.Handle;
        async void SelectNext(Preset preset)
        {
            if (exiting || paused) return;
            if (working) { pending = preset; return; }
            working = true;
            try
            {
                nint window = ModelSelector.FindWindow();
                if (!ModelSelector.IsForeground(window)) throw new InvalidOperationException("Focus Codex before pressing H, I, or J.");
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
                icon.Text = $"{preset.Position}: {preset.Model} {preset.Effort}";
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
                var item = menu.Items.Add($"{preset.Position}: {preset.Model} — {preset.Effort}");
                item.Enabled = false;
                assignmentItems.Add(item);
            }
        }
        void ShowSettings()
        {
            if (settingsWindow is { IsDisposed: false }) { settingsWindow.Activate(); settingsWindow.BringToFront(); return; }
            try
            {
                var form = new PresetConfigurationForm(Presets.All);
                settingsWindow = form;
                form.AssignmentsSaved += presets =>
                {
                    Presets.SetAll(presets);
                    UpdateAssignmentMenu();
                    Log(new { kind = "assignments-saved", assignments = Presets.All.Select(preset => new { preset.Position, preset.Model, preset.EffortPosition }).ToArray() });
                    Notify("H, I, and J model assignments saved.");
                };
                form.FormClosed += (_, _) => settingsWindow = null;
                form.Show();
                form.Activate();
            }
            catch (Exception error) { Notify(error.Message); }
        }
        UpdateAssignmentMenu();
        var pause = menu.Items.Add("Pause");
        pause.Click += (_, _) => { paused = !paused; pause.Text = paused ? "Resume" : "Pause"; pending = null; icon.Text = paused ? "Micro presets: paused" : "Micro model presets"; };
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Forms.Application.ExitThread());
        icon.MouseClick += (_, eventArgs) => { if (eventArgs.Button == MouseButtons.Left) ShowSettings(); };
        using var listener = new MicroInputListener((device, report) => {
            if (paused || exiting) return;
            foreach (var preset in decoder.Feed(device, report))
            {
                long now = Environment.TickCount64;
                if (lastPosition == preset.Position && now - lastPress < 300) continue;
                lastPosition = preset.Position; lastPress = now;
                Log(new { kind = "physical-press", preset.Position, preset.HardwareKey });
                dispatcher.BeginInvoke(() => SelectNext(preset));
            }
        }, Log);
        var registration = ThreadPool.RegisterWaitForSingleObject(stop, (_, _) => PostThreadMessage(threadId, 0x12, 0, 0), null, Timeout.Infinite, executeOnlyOnce: true);
        Log(new { kind = "running", pid = Environment.ProcessId, positions = new[] { "H", "I", "J" } });
        try { System.Windows.Forms.Application.Run(); }
        finally
        {
            exiting = true;
            registration.Unregister(null);
            pending = null;
            if (worker != null && !worker.HasExited) { worker.Kill(entireProcessTree: true); worker.WaitForExit(2000); }
            icon.Visible = false;
            dispatcher.Dispose();
            Log(new { kind = "stopped" });
            mutex.ReleaseMutex();
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint thread, uint message, nuint wParam, nint lParam);
}
