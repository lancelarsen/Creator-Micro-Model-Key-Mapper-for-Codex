using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MicroPresets;

internal sealed class PresetConfigurationForm : Form
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    private readonly Preset[] original, draft;
    private readonly KeypadView keypad = new() { Dock = DockStyle.Fill };
    private readonly ComboBox model = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox effort = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox enabled = new() { Text = "Override with a model preset", AutoSize = true };
    private readonly CheckBox automatic = new() { Text = "Refresh models when this window opens", AutoSize = true };
    private readonly Label keyTitle = Label("", 18, true), explanation = Label("", 10), status = Label("", 9), catalogStatus = Label("", 9);
    private readonly Button refresh = Button("Refresh from Codex"), save = Button("Save changes", true), cancel = Button("Cancel");
    private ModelCatalog catalog = ModelCatalog.Load();
    private int selected = 1;
    private bool loading, busy, dirty;
    internal event Action<Preset[]>? AssignmentsSaved;
    internal bool IsBusy => busy;

    internal PresetConfigurationForm(IEnumerable<Preset> assignments, bool preview = false)
    {
        original = Presets.WithAssignments(assignments); draft = original.ToArray();
        Text = "Creator Micro · Model keys";
        Font = new Font("Segoe UI", 10); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Palette.Background; ForeColor = Palette.Text; Icon = IconArtwork.Load();
        StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(980, 720); MinimumSize = new Size(850, 720);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(root);
        var header = Stack(); header.Margin = new Padding(0, 0, 0, 20);
        header.Controls.Add(Label("Creator Micro", 24, true)); header.Controls.Add(Label("Your models. One keypress away.", 11)); root.Controls.Add(header, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        root.Controls.Add(body, 0, 1); body.Controls.Add(keypad, 0, 0);
        keypad.Selected += index => { CaptureSelection(); selected = index; ShowSelection(); };
        var editor = Stack(); editor.AutoScroll = true; editor.Dock = DockStyle.Fill; editor.AutoSize = false;
        editor.Padding = new Padding(24, 16, 16, 16); editor.BackColor = Palette.Surface; editor.Margin = new Padding(24, 0, 0, 0);
        body.Controls.Add(editor, 1, 0); editor.Controls.Add(keyTitle);
        explanation.Margin = new Padding(3, 4, 3, 20); editor.Controls.Add(explanation); editor.Controls.Add(enabled);
        editor.Controls.Add(Field("MODEL", model));
        effort.Items.AddRange(Presets.EffortNames.Cast<object>().ToArray());
        editor.Controls.Add(Field("REASONING", effort));
        editor.Controls.Add(Label("Default keeps the key’s Codex action. An override replaces it with your model preset.", 9));
        refresh.Margin = new Padding(3, 20, 3, 8); editor.Controls.Add(refresh);
        catalogStatus.Margin = new Padding(3, 0, 3, 10); editor.Controls.Add(catalogStatus);
        automatic.Checked = catalog.RefreshOnOpen; editor.Controls.Add(automatic);
        editor.SizeChanged += (_, _) => {
            int width = Math.Max(200, editor.ClientSize.Width - editor.Padding.Horizontal - 12);
            foreach (Control control in editor.Controls) { control.MinimumSize = Size.Empty; control.MaximumSize = new Size(width, 0); if (control is TableLayoutPanel) { control.MinimumSize = new Size(width, 0); control.Width = width; } }
        };
        enabled.CheckedChanged += (_, _) => { if (!loading) { CaptureSelection(); ShowSelection(); } };
        model.TextChanged += (_, _) => { if (!loading) CaptureSelection(); };
        effort.SelectedIndexChanged += (_, _) => { if (!loading) CaptureSelection(); };
        automatic.CheckedChanged += (_, _) => dirty = true;
        refresh.Click += async (_, _) => await RefreshModels();
        var actions = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 16, 0, 16) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        status.Text = "Choose any of the seven lower keys to edit."; status.Anchor = AnchorStyles.Left; actions.Controls.Add(status, 0, 0);
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save); actions.Controls.Add(buttons, 1, 0); root.Controls.Add(actions, 0, 2);
        save.Click += async (_, _) => await SaveAssignments(); cancel.Click += (_, _) => Close(); CancelButton = cancel;
        var info = Stack(); info.BackColor = Palette.Surface; info.Padding = new Padding(16, 12, 16, 12);
        info.Controls.Add(Label("Made by Lance Larsen", 11, true));
        info.Controls.Add(Label("Built to put your favorite Codex models and reasoning levels one keypress away.", 9));
        var links = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
        links.Controls.Add(Link("LanceLarsen.com", "https://www.LanceLarsen.com"));
        links.Controls.Add(Link("Open source on GitHub ↗", "https://github.com/lancelarsen/micro-model-presets"));
        links.Controls.Add(Label("Independent community project", 9)); info.Controls.Add(links); root.Controls.Add(info, 0, 3);
        LoadModels(); ShowSelection();
        Load += (_, _) => {
            var area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(850 * DeviceDpi / 96, area.Width), Math.Min(720 * DeviceDpi / 96, area.Height));
            ClientSize = new Size(Math.Min(980 * DeviceDpi / 96, area.Width - 80), Math.Min(760 * DeviceDpi / 96, area.Height - 100));
            CenterToScreen();
        };
        Shown += async (_, _) => { if (!preview && catalog.RefreshOnOpen) await RefreshModels(); };
        FormClosing += (_, e) => {
            if (busy) { e.Cancel = true; return; }
            if (dirty && MessageBox.Show(this, "Discard your unsaved changes?", "Unsaved assignments", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true;
        };
    }
    private void CaptureSelection()
    {
        var edited = draft[selected] with { Enabled = enabled.Checked, Model = model.Text.Trim(), Effort = effort.Text, EffortPosition = effort.SelectedIndex + 1 };
        if (edited != draft[selected]) dirty = true;
        draft[selected] = edited; keypad.SetAssignments(draft, selected);
    }
    internal void VerifyEditor(string output)
    {
        System.IO.Directory.CreateDirectory(output);
        foreach (int index in Enumerable.Range(0, 7))
        {
            keypad.SelectForTest(index);
            if (selected != index || model.Text != draft[index].Model) throw new InvalidOperationException("Key selection failed.");
            enabled.Checked = !enabled.Checked;
            if (draft[index].Enabled != enabled.Checked || model.Enabled != enabled.Checked) throw new InvalidOperationException("Default/override toggle failed.");
            enabled.Checked = !enabled.Checked;
        }
        keypad.SelectForTest(1);
        Console.WriteLine($"DPI: {DeviceDpi}; screen: {Screen.FromControl(this).WorkingArea}; initial size: {ClientSize}");
        foreach (var size in new[] { new Size(980, 760), new Size(850, 720) })
        {
            ClientSize = new Size(size.Width * DeviceDpi / 96, size.Height * DeviceDpi / 96);
            System.Windows.Forms.Application.DoEvents();
            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            bitmap.Save(System.IO.Path.Combine(output, $"editor-{size.Width}.png"));
        }
        dirty = false;
        Console.WriteLine("PASS: seven key selections, default/override toggles, and two native window renders.");
    }
    private void ShowSelection()
    {
        loading = true; var preset = draft[selected]; keyTitle.Text = $"Key {preset.Position}";
        explanation.Text = Presets.DefaultActions[selected] + " · physical position";
        enabled.Checked = preset.Enabled; model.Text = preset.Model; effort.SelectedIndex = preset.EffortPosition - 1;
        model.Enabled = effort.Enabled = preset.Enabled; keypad.SetAssignments(draft, selected); loading = false;
    }
    private void LoadModels()
    {
        loading = true; string current = model.Text; model.Items.Clear();
        model.Items.AddRange(catalog.Models.Concat(draft.Select(p => p.Model)).Distinct().Cast<object>().ToArray()); model.Text = current;
        catalogStatus.Text = catalog.CheckedAt is {} date ? $"Checked {date.ToLocalTime():MMM d, h:mm tt} · {catalog.Models.Length} models" : "Starter list · refresh for your available models";
        loading = false;
    }
    private void SetBusy(bool value, string message)
    {
        busy = value; save.Enabled = cancel.Enabled = refresh.Enabled = keypad.Enabled = enabled.Enabled = automatic.Enabled = !value;
        model.Enabled = effort.Enabled = !value && enabled.Checked; status.Text = message;
    }
    private async Task RefreshModels()
    {
        SetBusy(true, "Reading model choices from Codex…");
        try {
            var models = await StaWork.Run(ModelSelector.ListAvailableModels);
            catalog = new ModelCatalog(models, DateTimeOffset.Now, catalog.RefreshOnOpen); catalog.Save(); LoadModels();
            status.Text = "Model list updated. Your assignments are unchanged.";
        } catch (Exception error) { status.Text = "Refresh unavailable. Your cached list is still available."; catalogStatus.Text = error.Message; }
        finally { SetBusy(false, status.Text); Activate(); }
    }
    private async Task SaveAssignments()
    {
        CaptureSelection(); SetBusy(true, "Saving assignments… Codex may open its keypad settings.");
        try {
            var result = Presets.WithAssignments(draft);
            if (!PresetStore.Load().SequenceEqual(original)) throw new InvalidOperationException("Assignments changed in another window. Close and reopen this editor before saving.");
            await StaWork.Run(() => { MicroBindings.SaveAssignments(original, result); return true; });
            Presets.SetAll(result); AssignmentsSaved?.Invoke(result);
            Array.Copy(result, original, result.Length); dirty = false;
            catalog = catalog with { RefreshOnOpen = automatic.Checked }; catalog.Save();
            status.Text = "Saved. Your keys are ready in Codex.";
        } catch (Exception error) { status.Text = "Could not complete save."; MessageBox.Show(this, error.Message, "Check assignments", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { SetBusy(false, status.Text); Activate(); }
    }
    private static FlowLayoutPanel Stack() => new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private static Label Label(string text, float size, bool bold = false) => new() { Text = text, AutoSize = true, ForeColor = bold ? Palette.Text : Palette.Muted, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(3, 3, 3, 5) };
    private static Button Button(string text, bool primary = false) => new() { Text = text, AutoSize = true, Padding = new Padding(12, 8, 12, 8), FlatStyle = FlatStyle.Flat, BackColor = primary ? Palette.Accent : Palette.Surface, ForeColor = primary ? Palette.Background : Palette.Text, Cursor = Cursors.Hand, Margin = new Padding(6, 0, 0, 0) };
    private static Control Field(string title, ComboBox input)
    {
        input.BackColor = Palette.Key; input.ForeColor = Palette.Text; input.FlatStyle = FlatStyle.Flat; input.AccessibleName = title;
        if (input.DropDownStyle == ComboBoxStyle.DropDownList)
        {
            input.DrawMode = DrawMode.OwnerDrawFixed;
            input.ItemHeight = input.Font.Height + 8;
            input.DrawItem += (_, e) => {
                using var background = new SolidBrush(Palette.Key);
                e.Graphics.FillRectangle(background, e.Bounds);
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, input.Items[e.Index]?.ToString() ?? "", input.Font, e.Bounds, Palette.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.DrawFocusRectangle();
            };
        }
        var field = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 14, 0, 0), Width = 300 };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize)); field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        field.Controls.Add(Label(title, 9, true), 0, 0); field.Controls.Add(input, 0, 1); return field;
    }
    private LinkLabel Link(string label, string url)
    {
        var link = new LinkLabel { Text = label, AutoSize = true, LinkColor = Palette.Accent, ActiveLinkColor = Palette.Text, VisitedLinkColor = Palette.Accent, Margin = new Padding(3, 4, 20, 0) };
        link.LinkClicked += (_, _) => { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception e) { status.Text = e.Message; } }; return link;
    }
}
