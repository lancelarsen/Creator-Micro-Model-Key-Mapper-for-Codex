using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MicroPresets;

internal sealed class PresetConfigurationForm : Form
{
    private static readonly (string Name, int Position)[] EffortChoices =
    [
        ("Low — lowest", 1), ("Medium", 2), ("High", 3), ("Extra High", 4), ("Maximum — highest", 5)
    ];

    private readonly List<(ComboBox Model, ComboBox Effort)> rows = [];
    private readonly Label status = new();
    private readonly Button refresh = new();

    internal event Action<Preset[]>? AssignmentsSaved;

    internal PresetConfigurationForm(IEnumerable<Preset> assignments) : this(assignments, Presets.KnownModels) { }

    private PresetConfigurationForm(IEnumerable<Preset> assignments, IEnumerable<string> availableModels)
    {
        Text = "Codex Micro model assignments";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 400);
        Size = new Size(720, 440);
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 10);
        Icon = IconArtwork.Load();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(24) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        var introduction = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        introduction.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        introduction.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        introduction.Controls.Add(new Label { Text = "Choose a model and reasoning level for each key", Font = new Font(Font, FontStyle.Bold), AutoSize = true }, 0, 0);
        introduction.Controls.Add(new Label { Text = "These are the physical H, I, and J positions on your Micro keypad.", AutoSize = true, ForeColor = Color.DimGray }, 0, 1);
        root.Controls.Add(introduction, 0, 0);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        for (int row = 1; row < 4; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));
        grid.Controls.Add(Header("POSITION"), 0, 0);
        grid.Controls.Add(Header("MODEL"), 1, 0);
        grid.Controls.Add(Header("REASONING"), 2, 0);

        var modelNames = availableModels.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var preset in Presets.WithAssignments(assignments))
        {
            if (!modelNames.Contains(preset.Model, StringComparer.OrdinalIgnoreCase)) modelNames.Add(preset.Model);
            int rowIndex = rows.Count + 1;
            grid.Controls.Add(new Label { Text = preset.Position, Font = new Font(Font, FontStyle.Bold), AutoSize = true, Anchor = AnchorStyles.Left }, 0, rowIndex);

            var model = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
            model.Items.AddRange(modelNames.Cast<object>().ToArray());
            model.Text = preset.Model;
            grid.Controls.Add(model, 1, rowIndex);

            var effort = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var choice in EffortChoices) effort.Items.Add(choice.Name);
            effort.SelectedIndex = Math.Clamp(preset.EffortPosition - 1, 0, EffortChoices.Length - 1);
            grid.Controls.Add(effort, 2, rowIndex);
            rows.Add((model, effort));
        }
        root.Controls.Add(grid, 0, 1);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        refresh.Text = "Refresh models from Codex";
        refresh.AutoSize = true;
        refresh.Anchor = AnchorStyles.Left;
        refresh.Click += (_, _) => RefreshModels();
        actions.Controls.Add(refresh, 0, 0);
        status.Text = "Current model choices are listed; refresh them from Codex when needed.";
        status.AutoSize = true;
        status.Anchor = AnchorStyles.Left;
        status.ForeColor = Color.DimGray;
        actions.Controls.Add(status, 1, 0);
        root.Controls.Add(actions, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button { Text = "Save assignments", AutoSize = true, DialogResult = DialogResult.None };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => SaveAssignments();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 3);
        CancelButton = cancel;
        AcceptButton = save;
    }

    private static Label Header(string text) => new() { Text = text, AutoSize = true, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 9, FontStyle.Bold), Anchor = AnchorStyles.Left | AnchorStyles.Bottom };

    private void SaveAssignments()
    {
        try
        {
            var result = rows.Select((row, index) =>
            {
                string model = row.Model.Text.Trim();
                if (string.IsNullOrWhiteSpace(model) || model.Length > 100) throw new InvalidDataException($"Enter a model name for {new[] { "H", "I", "J" }[index]}.");
                int position = EffortChoices[row.Effort.SelectedIndex].Position;
                string name = EffortChoices[row.Effort.SelectedIndex].Name;
                return new Preset("HIJ"[index].ToString(), $"ACT0{index + 7}", model, name, position);
            }).ToArray();
            var valid = Presets.WithAssignments(result);
            PresetStore.Save(valid);
            AssignmentsSaved?.Invoke(valid);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "Could not save model assignments", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RefreshModels()
    {
        refresh.Enabled = false;
        status.Text = "Reading the visible model list from Codex…";
        try
        {
            string[] models = ModelSelector.ListAvailableModels();
            if (models.Length == 0) throw new InvalidOperationException("Codex did not show any model choices.");
            foreach (var row in rows)
            {
                string selected = row.Model.Text;
                row.Model.Items.Clear();
                row.Model.Items.AddRange(models.Cast<object>().ToArray());
                if (!models.Contains(selected, StringComparer.OrdinalIgnoreCase)) row.Model.Items.Add(selected);
                row.Model.Text = selected;
            }
            status.Text = $"Loaded {models.Length} model choices from Codex.";
        }
        catch (Exception error) { status.Text = error.Message; }
        finally
        {
            refresh.Enabled = true;
            Activate();
            BringToFront();
        }
    }
}
