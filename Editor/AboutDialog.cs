using C = Microsoft.UI.Xaml.Controls;
using X = Microsoft.UI.Xaml;
using M = Microsoft.UI.Xaml.Media;

namespace CreatorMicroEditor;

internal static class AboutDialog
{
    private static M.SolidColorBrush Brush(byte r, byte g, byte b) => new(Windows.UI.Color.FromArgb(255, r, g, b));
    private static readonly M.SolidColorBrush Accent = Brush(177, 164, 255), Muted = Brush(171, 174, 188), Surface = Brush(40, 42, 50);
    private static C.TextBlock Text(string value, double size = 14, M.SolidColorBrush? color = null) => new() {
        Text = value, FontSize = size, Foreground = color ?? Brush(242, 241, 247), TextWrapping = X.TextWrapping.Wrap
    };
    internal static C.ContentDialog Create(X.XamlRoot root, string checkedAt, bool refreshOnOpen, bool launchOnStartup, bool enabled, Func<bool, bool, Task> changed)
    {
        var content = new C.StackPanel { Spacing = 20, MaxWidth = 400 };
        var links = new C.StackPanel { Spacing = 8 };
        links.Children.Add(Link("\uE77B", "Lance Larsen", "Creator · LanceLarsen.com", "https://www.LanceLarsen.com"));
        links.Children.Add(Link("\uE943", "Open Source", "Explore the project on GitHub", "https://github.com/lancelarsen/Creator-Micro-Model-Key-Mapper-for-Codex"));
        var preferences = new C.StackPanel { Spacing = 8 };
        var label = Text("Startup preferences", 14); label.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold; preferences.Children.Add(label);
        var toggle = new C.CheckBox { Content = "Refresh models when opening", IsChecked = refreshOnOpen, IsEnabled = enabled, Foreground = Brush(242, 241, 247) };
        var startup = new C.CheckBox { Content = "Launch on Startup", IsChecked = launchOnStartup, IsEnabled = enabled, Foreground = Brush(242, 241, 247) };
        var status = Text("", 12, Muted); status.Visibility = X.Visibility.Collapsed;
        bool updating = false;
        async void SavePreference(object sender, X.RoutedEventArgs args) {
            if (updating) return;
            updating = true; toggle.IsEnabled = startup.IsEnabled = false;
            try { await changed(toggle.IsChecked == true, startup.IsChecked == true); refreshOnOpen = toggle.IsChecked == true; launchOnStartup = startup.IsChecked == true; status.Text = ""; status.Visibility = X.Visibility.Collapsed; }
            catch (Exception error) { toggle.IsChecked = refreshOnOpen; startup.IsChecked = launchOnStartup; status.Text = error.Message; status.Visibility = X.Visibility.Visible; }
            finally { updating = false; toggle.IsEnabled = startup.IsEnabled = enabled; }
        }
        toggle.Checked += SavePreference; toggle.Unchecked += SavePreference;
        startup.Checked += SavePreference; startup.Unchecked += SavePreference;
        preferences.Children.Add(startup);
        preferences.Children.Add(toggle); preferences.Children.Add(Text(checkedAt, 12, Muted));
        preferences.Children.Add(status);
        var settingsTitle = Text("Settings", 24); settingsTitle.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        var settingsHeading = new C.StackPanel { Orientation = C.Orientation.Horizontal, Spacing = 12, HorizontalAlignment = X.HorizontalAlignment.Center };
        settingsHeading.Children.Add(new C.FontIcon { Glyph = "\uE713", FontFamily = new M.FontFamily("Segoe Fluent Icons"), FontSize = 26, Foreground = Accent });
        settingsHeading.Children.Add(settingsTitle); content.Children.Add(settingsHeading);
        content.Children.Add(new C.Border { Height = 1, Background = Brush(60, 62, 72) });
        content.Children.Add(new C.Border { Child = preferences, Padding = new X.Thickness(16), Background = Surface, CornerRadius = new X.CornerRadius(12) });
        content.Children.Add(new C.Border { Height = 1, Background = Brush(60, 62, 72), Margin = new X.Thickness(0, 4, 0, 4) });
        content.Children.Add(links);
        content.Children.Add(Text("Independent community project. Codex is by OpenAI.", 11, Muted));
        var closeStyle = new X.Style(typeof(C.Button));
        closeStyle.Setters.Add(new X.Setter(C.Control.BackgroundProperty, Brush(113, 99, 215)));
        closeStyle.Setters.Add(new X.Setter(C.Control.ForegroundProperty, Brush(255, 255, 255)));
        closeStyle.Setters.Add(new X.Setter(C.Control.CornerRadiusProperty, new X.CornerRadius(10)));
        var dialog = new C.ContentDialog { Content = content, CloseButtonText = "Close", CloseButtonStyle = closeStyle, XamlRoot = root, RequestedTheme = X.ElementTheme.Dark };
        dialog.Resources["ContentDialogBackground"] = Brush(30, 32, 39);
        dialog.Resources["ContentDialogTopOverlayBackground"] = Brush(30, 32, 39);
        dialog.Resources["ContentDialogCornerRadius"] = new X.CornerRadius(20);
        dialog.Resources["SystemControlHighlightAccentBrush"] = Accent;
        dialog.Opened += (_, _) => {
            // The standard footer reserves multiple columns; span them for one centered action.
            var close = FindClose(dialog);
            if (close != null && close.Parent is C.Grid buttons) {
                C.Grid.SetColumn(close, 0); C.Grid.SetColumnSpan(close, Math.Max(1, buttons.ColumnDefinitions.Count));
                close.HorizontalAlignment = X.HorizontalAlignment.Center; close.Width = 160;
            }
        };
        return dialog;
    }
    private static C.Button? FindClose(X.DependencyObject parent)
    {
        for (int i = 0; i < M.VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = M.VisualTreeHelper.GetChild(parent, i);
            if (child is C.Button { Name: "CloseButton" } close) return close;
            if (FindClose(child) is {} found) return found;
        }
        return null;
    }
    private static C.Button Link(string glyph, string title, string subtitle, string url)
    {
        var row = new C.Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new() { Width = X.GridLength.Auto });
        row.ColumnDefinitions.Add(new() { Width = new X.GridLength(1, X.GridUnitType.Star) });
        row.Children.Add(new C.FontIcon { Glyph = glyph, FontFamily = new M.FontFamily("Segoe Fluent Icons"), FontSize = 24, Foreground = Accent, VerticalAlignment = X.VerticalAlignment.Center });
        var copy = new C.StackPanel { Spacing = 4 };
        var name = Text(title, 15); name.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        copy.Children.Add(name); copy.Children.Add(Text(subtitle, 12, Muted));
        C.Grid.SetColumn(copy, 1); row.Children.Add(copy);
        var button = new C.Button { Content = row, Background = Surface, Foreground = Accent,
            BorderBrush = Brush(69, 70, 77), BorderThickness = new X.Thickness(1),
            Padding = new X.Thickness(16, 12, 16, 12), CornerRadius = new X.CornerRadius(12),
            HorizontalAlignment = X.HorizontalAlignment.Stretch, HorizontalContentAlignment = X.HorizontalAlignment.Stretch };
        button.Click += async (_, _) => await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        return button;
    }
}
