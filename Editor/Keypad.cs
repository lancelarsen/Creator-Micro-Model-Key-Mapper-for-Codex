using MicroPresets;
using Microsoft.Maui.Controls.Shapes;
namespace CreatorMicroEditor;

public sealed class Keypad : ContentView
{
    private readonly Grid grid = new() { RowSpacing = 10, ColumnSpacing = 10 };
    private readonly Button[] keys = new Button[7];
    private readonly Border[] caps = new Border[7];
    internal event Action<int>? Selected;
    public Keypad()
    {
        for (int i = 0; i < 4; i++) { grid.RowDefinitions.Add(new RowDefinition(GridLength.Star)); grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star)); }
        var board = new Border { Padding = 16, Stroke = Color.FromArgb("#3C3E47"), StrokeThickness = 2, StrokeShape = new RoundRectangle { CornerRadius = 24 }, Background = Gradient("#25272D", "#191A20"), Content = grid, Shadow = new Shadow { Brush = Colors.Black, Opacity = .4f, Radius = 18, Offset = new Point(0, 10) } };
        Content = board; MaximumWidthRequest = 520; HorizontalOptions = LayoutOptions.Fill;
        SizeChanged += (_, _) => { if (Width > 0) HeightRequest = Width; };
        Add(Knob(), 0, 0);
        Add(new Border { StrokeShape = new RoundRectangle { CornerRadius = 19 }, Stroke = Color.FromArgb("#444650"), BackgroundColor = Color.FromArgb("#222329"), Padding = 12, Content = Knob() }, 0, 3);
        var locations = new[] { (0,1), (0,2), (1,0), (1,1), (1,2), (1,3) };
        string[] colors = ["#AC9ADC", "#A3BBD9", "#CF9CA8", "#B4C49F", "#DBBC91", "#A9A1D7"];
        for (int i = 0; i < locations.Length; i++) {
            var light = new Ellipse { WidthRequest = 18, HeightRequest = 18, Fill = Color.FromArgb(colors[i]), Stroke = Color.FromArgb("#DAD5EC"), StrokeThickness = .5, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            var cap = new Border { StrokeShape = new RoundRectangle { CornerRadius = 19 }, Stroke = Color.FromArgb("#85858B"), Background = Gradient("#858589", "#626269"), Content = light, InputTransparent = true };
            SemanticProperties.SetDescription(cap, $"Codex colored key {i + 1}, unchanged");
            Add(cap, locations[i].Item1, locations[i].Item2);
        }
        var input = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, Padding = new Thickness(4, 14) };
        var lights = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        foreach (var color in new[] { "#A8BFF6", "#CDCDA0", "#CDCDA0" }) lights.Add(new Ellipse { WidthRequest = 5, HeightRequest = 5, Fill = Color.FromArgb(color) });
        input.Add(lights); var knob = Knob(); Grid.SetColumn(knob, 1); input.Add(knob); Add(input, 3, 0);
        for (int i = 0; i < 7; i++) {
            int index = i;
            var button = new Button { BackgroundColor = Colors.Transparent, TextColor = Colors.White, FontSize = 12, Padding = new Thickness(5), CornerRadius = 17, LineBreakMode = LineBreakMode.WordWrap };
            button.Clicked += (_, _) => Selected?.Invoke(index);
            keys[i] = button;
            caps[i] = new Border { StrokeShape = new RoundRectangle { CornerRadius = 19 }, StrokeThickness = 1.5, Content = button, Shadow = new Shadow { Brush = Colors.Black, Opacity = .35f, Radius = 3, Offset = new Point(0, 3) } };
            Add(caps[i], i < 4 ? 2 : 3, i < 4 ? i : i - 3);
        }
        Update(Presets.Defaults(), 1);
    }
    private void Add(View view, int row, int column) { Grid.SetRow(view, row); Grid.SetColumn(view, column); grid.Children.Add(view); }
    private static Brush Gradient(string a, string b) => new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(a), 0), new(Color.FromArgb(b), 1) }, new Point(0, 0), new Point(1, 1));
    private static View Knob() => new Border { Margin = 4, StrokeShape = new RoundRectangle { CornerRadius = 100 }, Stroke = Color.FromArgb("#444650"), Background = Gradient("#393B44", "#17181D"), Shadow = new Shadow { Brush = Colors.Black, Opacity = .4f, Radius = 5, Offset = new Point(2, 4) } };
    internal void SelectForTest(int index) => ((IButtonController)keys[index]).SendClicked();
    internal void Update(Preset[] presets, int selected)
    {
        for (int i = 0; i < 7; i++) {
            var p = presets[i];
            keys[i].Text = p.Enabled ? $"{p.Model.Replace("GPT-", "")}\n{p.Effort}" : $"{Presets.DefaultActions[i]}\n(default)";
            SemanticProperties.SetDescription(keys[i], $"{(i < 4 ? "Upper" : "Lower")} action row, position {(i < 4 ? i + 1 : i - 3)}. {keys[i].Text}. {(i == selected ? "Selected." : "")}");
            caps[i].Stroke = Color.FromArgb(i == selected ? "#C7B9FF" : "#464851");
            caps[i].StrokeThickness = i == selected ? 2.5 : 1;
            caps[i].Background = i == selected ? Gradient("#494154", "#2A2735") : Gradient("#303239", "#222329");
            keys[i].TextColor = Color.FromArgb(p.Enabled ? "#F3EDFF" : "#C3C5CE");
        }
    }
}
