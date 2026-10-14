using MicroPresets;
using System.Runtime.InteropServices.WindowsRuntime;
namespace CreatorMicroEditor;

public partial class MainPage
{
    private async Task VerifyUi(string output)
    {
        Directory.CreateDirectory(output);
        try {
            await SetPanel(true, false);
            for (int i = 0; i < 7; i++) {
                Keys.SelectForTest(i);
                if (selected != i) throw new Exception("Key click failed.");
                bool original = draft.Assignments[i].Enabled;
                ((IButtonController)(original ? DefaultMode : PresetMode)).SendClicked();
                if (draft.Assignments[i].Enabled == original || PresetFields.IsVisible == original) throw new Exception("Override toggle failed.");
                ((IButtonController)(original ? PresetMode : DefaultMode)).SendClicked();
            }
            if (draft.IsDirty) throw new Exception("Selection changed saved draft.");
            draft.Assignments[0] = draft.Assignments[0] with { Enabled = !draft.Assignments[0].Enabled };
            CancelClicked(null, EventArgs.Empty); await Task.Delay(700);
            if (panelOpen || PopupGroup.IsVisible || draft.IsDirty) throw new Exception("Cancel did not discard and close.");
            if (Math.Abs(Keys.X + Keys.Width / 2 - Width / 2) > 2) throw new Exception("Closed keyboard is not centered.");
            await Capture(output, "closed.png");
            Keys.SelectForTest(1); await Task.Delay(700);
            if (!panelOpen || !PopupGroup.IsVisible) throw new Exception("Key did not reopen editor.");
            selected = 1; ShowSelection(); Status.Text = "";
            foreach (int width in new[] { 1080, 880 }) {
                Window.Width = width; Window.Height = 670;
                await Task.Delay(1400);
                await Capture(output, $"maui-{width}.png");
                if (width == 1080) {
                    if (Math.Abs(Keys.Width - EditorPanel.Width) > 2 || Math.Abs(Keys.Height - EditorPanel.Height) > 2) throw new Exception("Panel must match keyboard size.");
                    if (Math.Abs(Keys.Y + Keys.Height / 2 - PopupGroup.Y - EditorPanel.Height / 2) > 2) throw new Exception("Editor is not vertically centered.");
                    foreach (var button in new[] { ModelButton, EffortButton }) {
                        ((IButtonController)button).SendClicked(); await Task.Delay(250);
                        if (activeDropdown == null || activeDropdown.Items.Count < 6 || activeDropdown.Items.OfType<Microsoft.UI.Xaml.Controls.ToggleMenuFlyoutItem>().Count(item => item.IsChecked) != 1) throw new Exception("Dropdown choices or current selection missing.");
                        if (((Microsoft.UI.Xaml.Controls.ToggleMenuFlyoutItem)activeDropdown.Items[0]).Text != (button == ModelButton ? Model.ItemsSource.Cast<string>().First() : Presets.EffortNames.Last())) throw new Exception("Dropdown order does not match the catalog or reasoning order.");
                        var root = ((Microsoft.UI.Xaml.FrameworkElement)Handler!.PlatformView!).XamlRoot;
                        var popup = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Last();
                        await Capture(output, button == ModelButton ? "model-dropdown.png" : "reasoning-dropdown.png", (Microsoft.UI.Xaml.FrameworkElement)popup.Child);
                        activeDropdown.Hide();
                    }
                }
                if (PageScroll.ContentSize.Height > PageScroll.Height + 1) await PageScroll.ScrollToAsync(0, PageScroll.ContentSize.Height, false).WaitAsync(TimeSpan.FromSeconds(2));
                await Task.Delay(300);
                await Capture(output, $"maui-{width}-bottom.png");
                if (PageScroll.ScrollY > 0) await PageScroll.ScrollToAsync(0, 0, false).WaitAsync(TimeSpan.FromSeconds(2));
            }
            Keys.SelectForTest(0); await Task.Delay(200); await Capture(output, "default-key.png");
            AboutClicked(null, EventArgs.Empty); await Task.Delay(500);
            if (aboutDialog == null) throw new Exception("About dialog did not open.");
            await Capture(output, "about.png", aboutDialog); aboutDialog.Hide();
            bool savingShown = false;
            await Run(async () => {
                savingShown = SaveSpinner.IsRunning && SaveSpinner.IsVisible && !Save.IsEnabled;
                await Task.Delay(300);
                await Capture(output, "saving.png");
                throw new IOException("Simulated save failure");
            }, "", saving: true);
            if (!savingShown) throw new Exception("Saving indicator missing or button enabled.");
            if (SaveSpinner.IsRunning || SaveSpinner.IsVisible || Save.Text != "Done") throw new Exception("Save failure did not reset spinner.");
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: seven key clicks, reversible draft toggles, two native MAUI renders including scrolled settings and About.");
        } catch (Exception e) { File.WriteAllText(Path.Combine(output, "result.txt"), e.ToString()); }
        finally { Application.Current!.Quit(); }
    }
    private async Task Capture(string output, string name, Microsoft.UI.Xaml.FrameworkElement? target = null)
    {
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
                var native = target ?? (Microsoft.UI.Xaml.FrameworkElement)Handler!.PlatformView!;
                await bitmap.RenderAsync(native);
                var pixels = await bitmap.GetPixelsAsync();
                using var stream = File.Create(Path.Combine(output, name));
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
                encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
                await encoder.FlushAsync();
    }
}
