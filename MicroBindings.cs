using System.Windows.Automation;
using System.IO;
using System.Text.Json;

namespace MicroPresets;

internal static class MicroBindings
{
    private static readonly HashSet<string> KeyNames = ["New chat", "Decrease reasoning effort", "Increase reasoning effort", "Fork chat", "Push to talk", "Send message", "Open your dot", "Assign any shortcut"];

    internal static AutomationElement Open()
    {
        nint window = ModelSelector.FindWindow();
        if (!ModelSelector.IsForeground(window)) throw new InvalidOperationException("Focus Codex before configuring the keys.");
        var initialRoot = AutomationElement.FromHandle(window);
        if (FindAny(initialRoot, ControlType.Window, "Edit keycap") != null) throw new InvalidOperationException("Close the keycap editor before configuring the presets.");
        System.Windows.Forms.SendKeys.SendWait("^,");
        var root = AutomationElement.FromHandle(window);
        var navigation = ModelSelector.WaitFor(() => FindAny(root, ControlType.Button, "Creator Micro"));
        if (navigation.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var scroll)) ((ScrollItemPattern)scroll).ScrollIntoView();
        ModelSelector.Activate(navigation);
        var firstKey = ModelSelector.WaitFor(() => FindAny(root, ControlType.Button, "New chat"));
        if (firstKey.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var keyScroll)) ((ScrollItemPattern)keyScroll).ScrollIntoView();
        ModelSelector.WaitFor(() => ModelSelector.FindVisible(root, ControlType.Button, name => name == "New chat"));
        return root;
    }

    internal static object Inspect()
    {
        var root = Open();
        return Snapshot(root);
    }

    internal static object Snapshot(AutomationElement root)
    {
        var keys = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
            .Cast<AutomationElement>().Where(element => KeyNames.Contains(element.Current.Name)).ToList();
        return keys.Select(element => new { element.Current.Name, rectangle = element.Current.BoundingRectangle.ToString() }).ToArray();
    }

    internal static List<AutomationElement> GetKeys(AutomationElement root)
    {
        var buttons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        var keys = buttons.Cast<AutomationElement>().Where(element => KeyNames.Contains(element.Current.Name) && element.Current.BoundingRectangle.Width > 0).ToList();
        var first = keys.Single(element => element.Current.Name == "New chat").Current.BoundingRectangle;
        keys = keys.Where(element => Math.Abs(element.Current.BoundingRectangle.Height - first.Height) < 8).ToList();
        // The existing preview's four-button action row identifies G/H/I/J.
        var row = keys.Where(element => Math.Abs(element.Current.BoundingRectangle.Top - first.Top) < 8).OrderBy(element => element.Current.BoundingRectangle.Left).ToList();
        if (row.Count != 4 || row[0].Current.Name != "New chat") throw new InvalidOperationException("The keypad preview does not have the expected G/H/I/J row.");
        var lower = keys.Where(element => element.Current.BoundingRectangle.Top > first.Bottom).OrderBy(element => element.Current.BoundingRectangle.Left).ToList();
        if (lower.Count != 3) throw new InvalidOperationException("The keypad preview does not have the expected K/L/M row.");
        return [..row, ..lower];
    }

    internal static AutomationElement? FindAny(AutomationElement root, ControlType type, string name)
    {
        var elements = root.FindAll(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, type), new PropertyCondition(AutomationElement.NameProperty, name)));
        return elements.Count == 1 ? elements[0] : null;
    }

    internal static object InspectEditor()
    {
        var root = AutomationElement.FromHandle(ModelSelector.FindWindow());
        return root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
            .Where(element => new[] { "Edit keycap", "EMPT1", "EMPT2", "EMPT3", "EMPT4", "Save", "Cancel" }.Contains(element.Current.Name))
            .Select(element => new { element.Current.Name, type = element.Current.ControlType.ProgrammaticName,
                patterns = element.GetSupportedPatterns().Select(pattern => pattern.ProgrammaticName).ToArray() }).ToArray();
    }

    internal static AutomationElement? FindNamed(AutomationElement root, string name)
    {
        var elements = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
        return elements.Count == 1 ? elements[0] : null;
    }

    internal static void CancelEditor()
    {
        var root = AutomationElement.FromHandle(ModelSelector.FindWindow());
        var dialog = FindAny(root, ControlType.Window, "Edit keycap");
        if (dialog != null) ModelSelector.Activate(FindAny(dialog, ControlType.Button, "Cancel")!);
    }

    internal static object Configure(string backupPath)
    {
        var root = Open();
        string[] before = GetKeys(root).Select(key => key.Current.Name).ToArray();
        string[] originals = ["Decrease reasoning effort", "Increase reasoning effort", "Fork chat"];
        for (int index = 1; index <= 3; index++)
            if (before[index] != originals[index - 1] && before[index] != "Assign any shortcut")
                throw new InvalidOperationException("One of the three preset positions has an unexpected assignment.");
        if (!File.Exists(backupPath)) File.WriteAllText(backupPath, JsonSerializer.Serialize(new { schemaVersion = 1, before, restoreKeycaps = new[] { "MIND-", "MIND+", "FORK" } }));
        string[] blanks = ["EMPT2", "EMPT3", "EMPT4"];
        var changed = new List<int>();
        try
        {
            for (int index = 1; index <= 3; index++)
            {
                changed.Add(index);
                AssignKeycap(root, index, blanks[index - 1]);
            }
            string[] after = GetKeys(root).Select(key => key.Current.Name).ToArray();
            if (new[] { 0, 4, 5, 6 }.Any(index => before[index] != after[index]) || after.Skip(1).Take(3).Any(name => name != "Assign any shortcut"))
                throw new InvalidOperationException("The preserved-key verification failed.");
            return new { before, after, preserved = new[] { "G", "K", "L", "M" } };
        }
        catch
        {
            CancelEditor();
            foreach (int index in changed) AssignKeycap(root, index, before[index] == "Assign any shortcut" ? blanks[index - 1] : new[] { "MIND-", "MIND+", "FORK" }[index - 1]);
            throw;
        }
    }

    private static void AssignKeycap(AutomationElement root, int index, string keycap)
    {
        ModelSelector.ClickElement(GetKeys(root)[index]);
        var dialog = ModelSelector.WaitFor(() => FindAny(root, ControlType.Window, "Edit keycap"));
        var option = ModelSelector.WaitFor(() => FindAny(dialog, ControlType.Button, keycap));
        if (option.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var scroll)) ((ScrollItemPattern)scroll).ScrollIntoView();
        ModelSelector.ClickElement(option);
        ModelSelector.WaitFor(() => ((TogglePattern)option.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.On ? option : null);
        Thread.Sleep(150);
        ModelSelector.ClickElement(FindAny(dialog, ControlType.Button, "Save") ?? throw new InvalidOperationException("The keycap editor has no Save action."));
        ModelSelector.WaitFor(() => FindAny(root, ControlType.Window, "Edit keycap") == null ? root : null);
    }

    internal static void Restore()
    {
        var root = Open();
        for (int index = 1; index <= 3; index++) AssignKeycap(root, index, new[] { "MIND-", "MIND+", "FORK" }[index - 1]);
    }

    internal static void ReturnToChat()
    {
        var root = AutomationElement.FromHandle(ModelSelector.FindWindow());
        ModelSelector.Activate(ModelSelector.FindVisible(root, ControlType.Button, name => name == "Home") ?? throw new InvalidOperationException("The Home action is unavailable."));
    }
}
