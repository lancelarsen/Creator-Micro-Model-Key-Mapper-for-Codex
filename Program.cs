using System.Text.Json;

namespace MicroPresets;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            if (args.Length == 0 || args.Contains("--listen") || args.Contains("--settings") || args.Contains("--restore-layout"))
                System.Windows.Forms.MessageBox.Show(error.Message, "Codex Micro presets", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        if (args.Contains("--stop")) { PresetRuntime.Stop(); return 0; }
        if (args.Length == 0 || args.Contains("--listen")) { Presets.SetAll(PresetStore.Load()); PresetRuntime.Run(PresetStore.LogPath); return 0; }
        if (args.Contains("--settings"))
        {
            Presets.SetAll(PresetStore.Load());
            using var form = new PresetConfigurationForm(Presets.All);
            form.AssignmentsSaved += Presets.SetAll;
            System.Windows.Forms.Application.Run(form);
            return 0;
        }
        if (args.Contains("--self-test")) { SelfTests.Run(); return 0; }
        if (args.Contains("--configure")) { ModelSelector.FocusApp(); Console.WriteLine(JsonSerializer.Serialize(MicroBindings.Configure(GetArg(args, "--backup") ?? PresetStore.LayoutBackupPath))); MicroBindings.ReturnToChat(); return 0; }
        if (args.Contains("--restore-layout")) { PresetRuntime.Stop(); ModelSelector.FocusApp(); MicroBindings.Restore(); MicroBindings.ReturnToChat(); return 0; }
        if (GetArg(args, "--select") is string position)
        {
            try
            {
                Presets.SetAll(PresetStore.Load());
                var preset = Presets.All.Single(p => p.Position.Equals(position, StringComparison.OrdinalIgnoreCase));
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
