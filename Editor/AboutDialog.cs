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
        links.Children.Add(Link(GitHubIcon(), "Open Source", "Explore the project on GitHub", "https://github.com/lancelarsen/Creator-Micro-Model-Key-Mapper-for-Codex"));
        links.Children.Add(Link("\uE765", "Creator Micro", "Explore the keyboard at Work Louder", "https://worklouder.cc/creator-micro-2"));
        var preferences = new C.StackPanel { Spacing = 8 };
        var label = Text("Starting Preferences", 14); label.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold; preferences.Children.Add(label);
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
        content.Children.Add(new C.Border { Child = preferences, Padding = new X.Thickness(16), Background = Surface, CornerRadius = new X.CornerRadius(12) });
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
    // GitHub Octicons mark; source and MIT license are in assets/github-mark.*.
    private static X.UIElement GitHubIcon()
    {
        var path = (X.Shapes.Path)X.Markup.XamlReader.Load("""
            <Path xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Data="M6.766 11.328c-2.063-.25-3.516-1.734-3.516-3.656 0-.781.281-1.625.75-2.188-.203-.515-.172-1.609.063-2.062.625-.078 1.468.25 1.968.703.594-.187 1.219-.281 1.985-.281.765 0 1.39.094 1.953.265.484-.437 1.344-.765 1.969-.687.218.422.25 1.515.046 2.047.5.593.766 1.39.766 2.203 0 1.922-1.453 3.375-3.547 3.64.531.344.89 1.094.89 1.954v1.625c0 .468.391.734.86.547C13.781 14.359 16 11.53 16 8.03 16 3.61 12.406 0 7.984 0 3.563 0 0 3.61 0 8.031a7.88 7.88 0 0 0 5.172 7.422c.422.156.828-.125.828-.547v-1.25c-.219.094-.5.156-.75.156-1.031 0-1.64-.562-2.078-1.609-.172-.422-.36-.672-.719-.719-.187-.015-.25-.093-.25-.187 0-.188.313-.328.625-.328.453 0 .844.281 1.25.86.313.452.64.655 1.031.655s.641-.14 1-.5c.266-.265.47-.5.657-.656"/>
            """);
        path.Fill = Accent;
        return new C.Viewbox { Width = 24, Height = 24, Child = path, VerticalAlignment = X.VerticalAlignment.Center };
    }
    private static C.Button Link(string glyph, string title, string subtitle, string url) =>
        Link(new C.FontIcon { Glyph = glyph, FontFamily = new M.FontFamily("Segoe Fluent Icons"), FontSize = 24, Foreground = Accent, VerticalAlignment = X.VerticalAlignment.Center }, title, subtitle, url);

    private static C.Button Link(X.UIElement icon, string title, string subtitle, string url)
    {
        var row = new C.Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new() { Width = X.GridLength.Auto });
        row.ColumnDefinitions.Add(new() { Width = new X.GridLength(1, X.GridUnitType.Star) });
        row.Children.Add(icon);
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
