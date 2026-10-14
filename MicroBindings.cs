using System.Windows.Automation;
using System.IO;
using System.Text.Json;

namespace MicroPresets;

internal static class MicroBindings
{
    private static readonly HashSet<string> KeyNames = ["New chat", "Decrease reasoning effort", "Increase reasoning effort", "Fork chat", "Push to talk", "Send message", "Open your dot", "Assign any shortcut", "Focus main chat"];

    internal static AutomationElement Open()
    {
        nint window = ModelSelector.FindWindow();
        var initialRoot = AutomationElement.FromHandle(window);
        if (FindAny(initialRoot, ControlType.Window, "Edit keycap") != null) throw new InvalidOperationException("Close the keycap editor before configuring the presets.");
        if (FindAny(initialRoot, ControlType.Button, "Creator Micro") == null)
        {
            var settings = ModelSelector.FindVisible(initialRoot, ControlType.MenuItem, name => name.StartsWith("Settings ", StringComparison.Ordinal));
            if (settings == null)
            {
                ModelSelector.Activate(ModelSelector.FindVisible(initialRoot, ControlType.Button, name => name == "Open profile menu")
                    ?? throw new InvalidOperationException("Codex's profile menu is unavailable."));
                settings = ModelSelector.WaitFor(() => ModelSelector.FindVisible(initialRoot, ControlType.MenuItem, name => name.StartsWith("Settings ", StringComparison.Ordinal)));
            }
            ModelSelector.Activate(settings);
        }
        var root = AutomationElement.FromHandle(window);
        var navigation = ModelSelector.WaitFor(() => FindAny(root, ControlType.Button, "Creator Micro"));
        if (navigation.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var scroll)) ((ScrollItemPattern)scroll).ScrollIntoView();
        ModelSelector.Activate(navigation);
        var firstKey = GetKeys(root)[0];
        if (firstKey.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var keyScroll)) ((ScrollItemPattern)keyScroll).ScrollIntoView();
        Thread.Sleep(350);
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
        => ModelSelector.WaitFor(() => { try { return GetKeysCore(root); } catch (ElementNotAvailableException) { return null; } catch (InvalidOperationException) { return null; } });

    private static List<AutomationElement> GetKeysCore(AutomationElement root)
    {
        var buttons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        var keys = buttons.Cast<AutomationElement>().Where(element => KeyNames.Contains(element.Current.Name) && element.Current.BoundingRectangle.Width > 0).ToList();
        var first = keys.OrderBy(element => element.Current.BoundingRectangle.Top).First().Current.BoundingRectangle;
        keys = keys.Where(element => Math.Abs(element.Current.BoundingRectangle.Height - first.Height) < 8).ToList();
        // The existing preview's four-button action row identifies G/H/I/J.
        var row = keys.Where(element => Math.Abs(element.Current.BoundingRectangle.Top - first.Top) < 8).OrderBy(element => element.Current.BoundingRectangle.Left).ToList();
        if (row.Count != 4) throw new InvalidOperationException("The keypad preview does not have the expected G/H/I/J row.");
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
        if (dialog != null)
        {
            ModelSelector.Activate(FindAny(dialog, ControlType.Button, "Cancel")!);
            ModelSelector.WaitFor(() => FindAny(root, ControlType.Window, "Edit keycap") == null ? root : null);
            Thread.Sleep(150);
        }
    }

    private static void AssignKeycap(AutomationElement root, int index, string keycap, string? action = null)
    {
        ModelSelector.Activate(GetKeys(root)[index]);
        var dialog = ModelSelector.WaitFor(() => FindAny(root, ControlType.Window, "Edit keycap"));
        var option = ModelSelector.WaitFor(() => FindAny(dialog, ControlType.Button, keycap));
        if (option.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var scroll)) ((ScrollItemPattern)scroll).ScrollIntoView();
        ModelSelector.Activate(option);
        ModelSelector.WaitFor(() => ((TogglePattern)option.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.On ? option : null);
        if (action != null)
        {
            Thread.Sleep(300);
            ModelSelector.Activate(FindAny(dialog, ControlType.Button, "Choose")!);
            Thread.Sleep(250);
            var menuItem = ModelSelector.WaitFor(() => {
                var choices = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)).Cast<AutomationElement>().ToArray();
                var exact = choices.Where(e => e.Current.Name == action ||
                    e.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))?.Current.Name == action).ToArray();
                if (exact.Length == 1) return exact[0];
                var prefixed = choices.Where(e => e.Current.Name.StartsWith(action + " ", StringComparison.Ordinal)).ToArray();
                return prefixed.Length == 1 ? prefixed[0] : choices.SingleOrDefault(e => e.Current.Name == "Use keycap default");
            });
            ModelSelector.Activate(menuItem);
            ModelSelector.WaitFor(() => FindAny(dialog, ControlType.Text, action));
        }
        Thread.Sleep(150);
        ModelSelector.Activate(FindAny(dialog, ControlType.Button, "Save") ?? throw new InvalidOperationException("The keycap editor has no Save action."));
        ModelSelector.WaitFor(() => FindAny(root, ControlType.Window, "Edit keycap") == null ? root : null);
    }

    private sealed record NativeAssignment(string Keycap, string Action);
    // Keep the keycap and action together; a blank icon can itself have a native action.
    internal static void SaveAssignments(Preset[] before, Preset[] after, bool syncOverrides = false)
    {
        using var operation = new CodexOperation();
        before = Presets.WithAssignments(before);
        after = Presets.WithAssignments(after);
        var changed = Enumerable.Range(0, 7).Where(i => before[i].Enabled != after[i].Enabled || (syncOverrides || !File.Exists(PresetStore.SettingsPath)) && after[i].Enabled).ToArray();
        if (changed.Length == 0) { PresetStore.Save(after); return; }
        string path = Path.Combine(PresetStore.DataDirectory, "original-actions.json");
        var originals = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, NativeAssignment>>(File.ReadAllText(path)) ?? throw new InvalidDataException("The original key assignments could not be read.")
            : new Dictionary<string, NativeAssignment>();
        var root = Open();
        var rollback = new Dictionary<int, NativeAssignment>();
        var currentAssignments = new Dictionary<int, NativeAssignment>();
        var applied = new List<int>();
        string stage = "reading existing keycaps";
        try
        {
            // Capture every affected key before making the first change.
            foreach (int index in Enumerable.Range(0, 7))
            {
                stage = $"opening key {after[index].Position}";
                var key = GetKeys(root)[index];
                string nativeAction = key.Current.Name == "Assign any shortcut" ? "Unassigned" : key.Current.Name;
                ModelSelector.Activate(key);
                var dialog = ModelSelector.WaitFor(() => FindAny(root, ControlType.Window, "Edit keycap"));
                string selected = dialog.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
                    .Cast<AutomationElement>().Where(e => e.TryGetCurrentPattern(TogglePattern.Pattern, out var p) && ((TogglePattern)p).Current.ToggleState == ToggleState.On)
                    .Select(e => e.Current.Name).Single();
                currentAssignments[index] = new NativeAssignment(selected, nativeAction);
                CancelEditor();
                if (!changed.Contains(index)) continue;
                rollback[index] = currentAssignments[index];
                string position = after[index].Position;
                if (after[index].Enabled && (!before[index].Enabled || !originals.ContainsKey(position)))
                {
                    originals[position] = before[index].Enabled && index is >= 1 and <= 3 && nativeAction == "Unassigned"
                        ? new NativeAssignment(new[] { "MIND-", "MIND+", "FORK" }[index - 1], new[] { "Decrease reasoning effort", "Increase reasoning effort", "Fork chat" }[index - 1])
                        : rollback[index];
                }
                else if (!originals.ContainsKey(position))
                {
                    // The first version cleared H/I/J before the per-key backup existed.
                    if (index is >= 1 and <= 3 && selected.StartsWith("EMPT", StringComparison.Ordinal))
                        originals[position] = new NativeAssignment(new[] { "MIND-", "MIND+", "FORK" }[index - 1], new[] { "Decrease reasoning effort", "Increase reasoning effort", "Fork chat" }[index - 1]);
                    else throw new InvalidOperationException($"The original action for {position} is unknown. Restore it in Codex first.");
                }
            }
            Directory.CreateDirectory(PresetStore.DataDirectory);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(originals));
            File.Move(path + ".tmp", path, true);
            foreach (int index in changed)
            {
                applied.Add(index);
                stage = $"assigning key {after[index].Position}";
                // Reusing a keycap swaps its physical position in Codex. Keep each icon and
                // replace its native action with a harmless composer-focus action instead.
                // Built-in keycaps do not offer an Unassigned action.
                var target = after[index].Enabled ? rollback[index] with { Action = "Focus main chat" } : originals[after[index].Position];
                AssignKeycap(root, index, target.Keycap, target.Action);
                string expected = target.Action == "Unassigned" ? "Assign any shortcut" : target.Action;
                ModelSelector.WaitFor(() => GetKeys(root)[index].Current.Name == expected ? root : null);
            }
            var finalKeys = GetKeys(root);
            foreach (int index in Enumerable.Range(0, 7).Except(changed))
            {
                string expected = currentAssignments[index].Action == "Unassigned" ? "Assign any shortcut" : currentAssignments[index].Action;
                if (finalKeys[index].Current.Name != expected) throw new InvalidOperationException($"Preserved key {after[index].Position} changed unexpectedly. Restoring assignments.");
            }
            PresetStore.Save(after);
        }
        catch (Exception error)
        {
            try
            {
                CancelEditor();
                foreach (int index in applied.AsEnumerable().Reverse()) AssignKeycap(root, index, rollback[index].Keycap, rollback[index].Action);
            }
            catch (Exception restoreError)
            {
                throw new InvalidOperationException("Assignments were not saved. Some Codex keys could not be restored; check Creator Micro settings. " + restoreError.Message, error);
            }
            throw new InvalidOperationException($"{stage}: {error.Message}", error);
        }
        finally { try { ReturnToChat(); } catch { /* Do not hide the save/rollback result. */ } }
    }

    internal static void ReturnToChat()
    {
        var root = AutomationElement.FromHandle(ModelSelector.FindWindow());
        ModelSelector.Activate(ModelSelector.FindVisible(root, ControlType.Button, name => name == "Home") ?? throw new InvalidOperationException("The Home action is unavailable."));
        ModelSelector.WaitFor(() => FindAny(root, ControlType.Button, "Creator Micro") == null ? root : null);
        Thread.Sleep(150);
    }
}
