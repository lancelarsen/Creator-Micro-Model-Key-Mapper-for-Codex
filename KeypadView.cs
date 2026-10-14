using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MicroPresets;

internal static class Palette
{
    internal static readonly Color Background = Color.FromArgb(24, 25, 28), Surface = Color.FromArgb(32, 34, 39), Key = Color.FromArgb(43, 45, 51);
    internal static readonly Color Border = Color.FromArgb(66, 69, 78), Text = Color.FromArgb(243, 244, 246), Muted = Color.FromArgb(173, 178, 189), Accent = Color.FromArgb(169, 185, 245);
}

internal sealed class KeypadView : UserControl
{
    private readonly TableLayoutPanel grid = new() { ColumnCount = 4, RowCount = 4, BackColor = Palette.Surface, Padding = new Padding(10) };
    private readonly Keycap[] keys = new Keycap[7];
    private readonly Label caption = new() { Text = "Colored chat keys stay connected to Codex.", ForeColor = Palette.Muted, TextAlign = ContentAlignment.MiddleCenter };
    internal event Action<int>? Selected;
    internal void SelectForTest(int index) => keys[index].PerformClick();
    internal KeypadView()
    {
        DoubleBuffered = true;
        for (int i = 0; i < 4; i++) { grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25)); }
        Controls.Add(grid); Controls.Add(caption);
        grid.Controls.Add(new Keycap { Decorative = true, Knob = true, Text = "Volume", Dock = DockStyle.Fill }, 0, 0);
        grid.Controls.Add(new Keycap { Decorative = true, Knob = true, Dock = DockStyle.Fill }, 3, 0);
        Color[] colors = [Color.FromArgb(169,151,224), Color.FromArgb(150,173,229), Color.FromArgb(126,191,180), Color.FromArgb(214,191,130), Color.FromArgb(216,153,167), Color.FromArgb(151,183,208)];
        for (int i = 0; i < 6; i++) grid.Controls.Add(new Keycap { Decorative = true, Indicator = colors[i], Text = $"Chat {i + 1}", Dock = DockStyle.Fill }, i < 2 ? i + 1 : i - 2, i < 2 ? 0 : 1);
        for (int i = 0; i < 7; i++)
        {
            int index = i;
            var key = new Keycap { Dock = DockStyle.Fill, Position = Presets.Positions[i], TabIndex = i };
            key.Click += (_, _) => Selected?.Invoke(index);
            keys[i] = key;
            grid.Controls.Add(key, i < 4 ? i : i - 3, i < 4 ? 2 : 3);
        }
        grid.Controls.Add(new Keycap { Decorative = true, Knob = true, Text = "Input 4", Dock = DockStyle.Fill }, 0, 3);
    }
    internal void SetAssignments(Preset[] presets, int selected)
    {
        for (int i = 0; i < 7; i++)
        {
            keys[i].Text = presets[i].Enabled ? presets[i].Model.Replace("GPT-", "") : Presets.DefaultActions[i];
            keys[i].Detail = presets[i].Enabled ? presets[i].Effort : "(default)";
            keys[i].IsSelected = i == selected; keys[i].IsOverride = presets[i].Enabled;
            keys[i].AccessibleName = $"Key {presets[i].Position}: {keys[i].Text}, {keys[i].Detail}";
            keys[i].Invalidate();
        }
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        int captionHeight = (int)(38 * DeviceDpi / 96f);
        int size = Math.Max(0, Math.Min(ClientSize.Width, ClientSize.Height - captionHeight));
        grid.SetBounds((ClientSize.Width - size) / 2, 0, size, size);
        caption.SetBounds(0, size, ClientSize.Width, captionHeight);
    }
}

internal sealed class Keycap : Button
{
    internal string Position = "", Detail = "";
    internal bool Decorative, Knob, IsSelected, IsOverride;
    internal Color Indicator;
    private bool hovered;
    internal Keycap()
    {
        FlatStyle = FlatStyle.Flat; Margin = new Padding(4);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnCreateControl() { base.OnCreateControl(); TabStop = !Decorative; Cursor = Decorative ? Cursors.Default : Cursors.Hand; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Palette.Surface);
        var rect = new Rectangle(2, 2, Width - 5, Height - 5);
        if (rect.Width < 10 || rect.Height < 10) return;
        using var path = new GraphicsPath();
        int radius = 20 * DeviceDpi / 96;
        if (Knob) path.AddEllipse(rect); else path.AddRoundedRectangle(rect, new Size(radius, radius));
        using var brush = new SolidBrush(Decorative && !Knob ? Color.FromArgb(77,79,85) : hovered && !Decorative ? Color.FromArgb(57,61,73) : Palette.Key);
        g.FillPath(brush, path);
        using var pen = new Pen(IsSelected ? Palette.Accent : Palette.Border, IsSelected ? 2 : 1); g.DrawPath(pen, path);
        if (Decorative)
        {
            if (!Knob) { using var dot = new SolidBrush(Indicator); g.FillEllipse(dot, Width / 2 - 7, Height / 2 - 15, 14, 14); }
            using var font = new Font("Segoe UI", 8);
            TextRenderer.DrawText(g, Text, font, new Rectangle(5, Height / 2 + 8, Width - 10, 24), Palette.Muted, TextFormatFlags.HorizontalCenter);
            return;
        }
        float scale = Math.Clamp(Width / (100f * DeviceDpi / 96), .85f, 1.1f);
        using var position = new Font("Segoe UI", 10 * scale, FontStyle.Bold);
        using var title = new Font("Segoe UI", 9 * scale, FontStyle.Bold);
        using var detail = new Font("Segoe UI", 8 * scale);
        int inset = 10 * DeviceDpi / 96;
        TextRenderer.DrawText(g, Position, position, new Rectangle(inset, inset, Width - 2 * inset, position.Height + 4), IsOverride ? Palette.Accent : Palette.Muted);
        TextRenderer.DrawText(g, Text, title, new Rectangle(7, Height / 2 - title.Height / 2, Width - 14, title.Height * 2 + 4), Palette.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        TextRenderer.DrawText(g, Detail, detail, new Rectangle(5, Height - inset - detail.Height - 4, Width - 10, detail.Height + 4), Palette.Muted, TextFormatFlags.HorizontalCenter);
        if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(rect, -6, -6), Palette.Accent, Palette.Key);
    }
}
