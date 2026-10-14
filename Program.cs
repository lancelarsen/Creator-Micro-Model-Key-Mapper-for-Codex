using System.Text.Json;

namespace MicroPresets;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (GetArg(args, "--result-log") is string log)
        {
            var writer = new System.IO.StreamWriter(log) { AutoFlush = true };
            Console.SetOut(writer); Console.SetError(writer);
        }
        try { return Run(args); }
        catch (Exception error)
        {
            Console.Error.WriteLine(args.Any(a => a is "--inspect-bindings" or "--verify-bindings") ? error.ToString() : error.Message);
            if (args.Length == 0 || args.Contains("--listen") || args.Contains("--settings") || args.Contains("--restore-layout"))
                System.Windows.Forms.MessageBox.Show(error.Message, "Codex Micro presets", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles();
        if (GetArg(args, "--ui-check") is string output)
        {
            using var preview = new PresetConfigurationForm(Presets.Defaults(), preview: true);
            preview.Show();
            preview.VerifyEditor(output);
            return 0;
        }
        if (args.Contains("--stop")) { PresetRuntime.Stop(); return 0; }
        if (args.Length == 0 || args.Contains("--listen")) { Presets.SetAll(PresetStore.Load()); PresetRuntime.Run(PresetStore.LogPath, args.Contains("--settings")); return 0; }
        if (args.Contains("--settings"))
        {
            Presets.SetAll(PresetStore.Load());
            using var form = new PresetConfigurationForm(Presets.All);
            form.AssignmentsSaved += Presets.SetAll;
            System.Windows.Forms.Application.Run(form);
            return 0;
        }
        if (args.Contains("--self-test")) { SelfTests.Run(); return 0; }
        if (GetArg(args, "--default-key") is string defaultKey)
        {
            if (!Presets.Positions.Contains(defaultKey)) throw new ArgumentException("Choose a key from G through M.");
            var current = PresetStore.Load();
            MicroBindings.SaveAssignments(current, current.Select(p => p.Position == defaultKey ? p with { Enabled = false } : p).ToArray());
            Console.WriteLine(defaultKey + " restored to default."); return 0;
        }
        if (args.Contains("--verify-bindings"))
        {
            var before = PresetStore.Load();
            var positions = (GetArg(args, "--positions") ?? "GHIJKLM").Select(position => Array.IndexOf(Presets.Positions, position.ToString())).ToArray();
            if (positions.Any(index => index < 0)) throw new ArgumentException("Choose positions G through M.");
            foreach (int index in positions)
            {
                var changed = before.ToArray(); changed[index] = changed[index] with { Enabled = !changed[index].Enabled };
                MicroBindings.SaveAssignments(before, changed);
                try { Console.WriteLine($"PASS: {changed[index].Position} switched {(changed[index].Enabled ? "to preset" : "to default")}"); }
                finally { MicroBindings.SaveAssignments(changed, before); }
                Console.WriteLine($"PASS: {changed[index].Position} original assignment restored");
            }
            return 0;
        }
        if (args.Contains("--inspect-bindings")) { ModelSelector.FocusApp(); Console.WriteLine(JsonSerializer.Serialize(MicroBindings.Inspect())); return 0; }
        if (args.Contains("--refresh-models"))
        {
            var models = ModelSelector.ListAvailableModels();
            (ModelCatalog.Load() with { Models = models, CheckedAt = DateTimeOffset.Now }).Save();
            Console.WriteLine(JsonSerializer.Serialize(models)); return 0;
        }
        if (args.Contains("--configure")) { var presets = PresetStore.Load(); MicroBindings.SaveAssignments(presets, presets, syncOverrides: true); return 0; }
        if (args.Contains("--restore-layout")) { PresetRuntime.Stop(); var presets = PresetStore.Load(); MicroBindings.SaveAssignments(presets, presets.Select(p => p with { Enabled = false }).ToArray()); return 0; }
        if (GetArg(args, "--select") is string position)
        {
            try
            {
                Presets.SetAll(PresetStore.Load());
                var preset = Presets.All.Single(p => p.Position.Equals(position, StringComparison.OrdinalIgnoreCase));
                if (!preset.Enabled) throw new InvalidOperationException("This key uses its default Codex action.");
                Console.WriteLine(JsonSerializer.Serialize(new { kind = "selected", preset.Position, selected = ModelSelector.Select(preset) }));
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        }
        throw new ArgumentException("Unknown command-line option. Start without arguments to run the Micro model presets app.");
    }

    internal static string? GetArg(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
