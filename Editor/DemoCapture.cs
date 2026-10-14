using System.Diagnostics;

namespace CreatorMicroEditor;

public partial class MainPage
{
    // Record real native rendering with example drafts. Never contacts the helper or saves bindings.
    private async Task CaptureDemo(string output)
    {
        Directory.CreateDirectory(output);
        int frame = 0;
        async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) {
                var timer = Stopwatch.StartNew();
                await Capture(output, $"frame{frame++:D4}.png");
                await Task.Delay(Math.Max(1, 83 - (int)timer.ElapsedMilliseconds));
            }
        }
        try {
            Window.Width = 880; Window.Height = 670;
            await Task.Delay(700);
            Status.Text = "";
            await Frames(18);
            selected = 1; ShowSelection();
            var opening = SetPanel(true);
            await Frames(24); await opening;
            await Capture(output, "main.png");
            Effort.SelectedIndex = Array.IndexOf(MicroPresets.Presets.EffortNames, "High");
            await Frames(18);
            Effort.SelectedIndex = Array.IndexOf(MicroPresets.Presets.EffortNames, "Extra High");
            await Frames(18);
            selected = 2; ShowSelection(); await Frames(18);
            selected = 3; ShowSelection(); await Frames(18);
            selected = 0; ShowSelection(); await Frames(18);
            draft.Discard(); ShowSelection(); Status.Text = "";
            var closing = SetPanel(false);
            await Frames(24); await closing;
            await Frames(18);
            AboutClicked(null, EventArgs.Empty); await Task.Delay(500);
            if (aboutDialog == null) throw new InvalidOperationException("Settings did not open.");
            await Capture(output, "settings.png", aboutDialog);
            aboutDialog.Hide();
            File.WriteAllText(Path.Combine(output, "capture.txt"), $"Captured {frame} native frames at a target of 12 fps. Example drafts only; no helper operations.");
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "error.txt"), error.ToString()); }
        finally { Application.Current!.Quit(); }
    }
}
