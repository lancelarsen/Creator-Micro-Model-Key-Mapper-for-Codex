using MicroPresets;
using System.Runtime.InteropServices.WindowsRuntime;
namespace CreatorMicroEditor;

public partial class MainPage
{
    private async Task VerifyUi(string output)
    {
        Directory.CreateDirectory(output);
        try {
            for (int i = 0; i < 7; i++) {
                Keys.SelectForTest(i);
                if (selected != i) throw new Exception("Key click failed.");
                bool original = draft.Assignments[i].Enabled;
                Override.IsToggled = !original;
                if (draft.Assignments[i].Enabled == original || PresetFields.IsVisible == original) throw new Exception("Override toggle failed.");
                Override.IsToggled = original;
            }
            if (draft.IsDirty) throw new Exception("Selection changed saved draft.");
            selected = 1; ShowSelection(); Status.Text = "Ready. Select a key to edit.";
            foreach (int width in new[] { 1080, 680 }) {
                Window.Width = width; Window.Height = 790;
                await Task.Delay(1400);
                await Capture(output, $"maui-{width}.png");
                await PageScroll.ScrollToAsync(0, PageScroll.ContentSize.Height, false);
                await Task.Delay(300);
                await Capture(output, $"maui-{width}-bottom.png");
                await PageScroll.ScrollToAsync(0, 0, false);
            }
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: seven key clicks, reversible draft toggles, two native MAUI renders including scrolled settings and About.");
        } catch (Exception e) { File.WriteAllText(Path.Combine(output, "result.txt"), e.ToString()); }
        finally { Application.Current!.Quit(); }
    }
    private async Task Capture(string output, string name)
    {
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
                var native = (Microsoft.UI.Xaml.FrameworkElement)Handler!.PlatformView!;
                await bitmap.RenderAsync(native);
                var pixels = await bitmap.GetPixelsAsync();
                using var stream = File.Create(Path.Combine(output, name));
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
                encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
                await encoder.FlushAsync();
    }
}
