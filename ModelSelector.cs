using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace MicroPresets;

internal static class ModelSelector
{
    internal sealed record PowerState(string Model, string Label, int Position, int Total);
    internal static PowerState? ParsePower(string text, string expectedModel)
    {
        var match = Regex.Match(text, "^" + Regex.Escape(expectedModel) + @" (.+), (\d+) of (\d+)\.$");
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out int position) || !int.TryParse(match.Groups[3].Value, out int total)
            || position < 1 || position > total || total > 10) return null;
        return new PowerState(expectedModel, match.Groups[1].Value, position, total);
    }

    internal static string Select(Preset preset)
    {
        nint window = FindWindow();
        RequireForeground(window);
        var root = AutomationElement.FromHandle(window);
        var composer = FindVisible(root, ControlType.Button, name => name.StartsWith("GPT-", StringComparison.Ordinal) || name.StartsWith("Default", StringComparison.Ordinal) || name == "Select effort")
            ?? throw new InvalidOperationException("The active page does not have a model selector.");
        var document = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
        string documentName = document.Current.Name;
        void CheckContext()
        {
            RequireForeground(window);
            if (document.Current.Name != documentName || composer.Current.IsOffscreen) throw new InvalidOperationException("The active chat changed during preset selection.");
        }
        bool opened = false;
        string stage = "opening controls";
        try
        {
            opened = true;
            if (FindVisible(root, ControlType.RadioButton, name => name == preset.Model) == null)
            {
                OpenControls();
                CheckContext();
                Activate(WaitFor(() => FindVisible(root, ControlType.MenuItem, name => name == "Select model")));
            }
            var model = WaitFor(() => FindVisible(root, ControlType.RadioButton, name => name == preset.Model));
            CheckContext();
            stage = "selecting model";
            Activate(model);
            stage = "reading reasoning status";
            WaitFor(() => FindVisible(root, ControlType.MenuItem, name => name == "Power"));
            var state = WaitFor(() => ReadPower(root, preset.Model));
            int targetPosition = Math.Min(preset.EffortPosition, state.Total);
            Thread.Sleep(150);
            for (int attempt = 0; state.Position != targetPosition && attempt < 10; attempt++)
            {
                int previous = state.Position;
                stage = $"adjusting reasoning from {previous} to {preset.EffortPosition}";
                CheckContext();
                var power = WaitFor(() => FindVisible(root, ControlType.MenuItem, name => name == "Power"));
                var track = power.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group))
                    ?? throw new InvalidOperationException("The reasoning slider is unavailable.");
                var rectangle = track.Current.BoundingRectangle;
                if (rectangle.Width <= 0 || rectangle.Height <= 0) throw new InvalidOperationException("The reasoning slider has no clickable area.");
                double inset = rectangle.Height / 2;
                double fraction = state.Total == 1 ? 0 : (double)(targetPosition - 1) / (state.Total - 1);
                ClickPoint(window, (int)Math.Round(rectangle.Left + inset + fraction * (rectangle.Width - 2 * inset)), (int)Math.Round(rectangle.Top + rectangle.Height / 2), track, power);
                state = WaitFor(() => { var current = ReadPower(root, preset.Model); return current?.Position != previous ? current : null; });
            }
            if (state.Position != targetPosition) throw new InvalidOperationException("Reasoning verification failed.");
            CheckContext();
            PressKey(0x1B);
            opened = false;
            stage = "verifying composer label";
            string finalLabel = WaitFor(() => FindVisible(root, ControlType.Button, name => name == preset.Model + " " + state.Label || name == preset.Model + " " + preset.Effort)).Current.Name;
            return finalLabel;
        }
        catch (TimeoutException error) { throw new TimeoutException(error.Message + " Stage: " + stage, error); }
        finally
        {
            if (opened && GetForegroundWindow() == window && document.Current.Name == documentName) PressKey(0x1B);
        }
    }

    private static PowerState? ReadPower(AutomationElement root, string model)
    {
        var menu = FindVisible(root, ControlType.Menu, _ => true);
        if (menu == null) return null;
        var texts = menu.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
        foreach (AutomationElement text in texts)
        {
            try { var state = ParsePower(text.Current.Name, model); if (state != null) return state; }
            catch (ElementNotAvailableException) { }
        }
        return null;
    }
    internal static void OpenControls()
    {
        nint window = FindWindow();
        RequireForeground(window);
        var root = AutomationElement.FromHandle(window);
        if (FindVisible(root, ControlType.MenuItem, name => name == "Select model") != null) return;
        var button = FindVisible(root, ControlType.Button, name => name.StartsWith("GPT-", StringComparison.Ordinal) || name.StartsWith("Default", StringComparison.Ordinal) || name == "Select effort")
            ?? throw new InvalidOperationException("The active page does not have a model selector.");
        Activate(button);
        WaitFor(() => FindVisible(root, ControlType.MenuItem, name => name == "Select model"));
    }

    internal static void Activate(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expand)) ((ExpandCollapsePattern)expand).Expand();
        else if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) ((InvokePattern)invoke).Invoke();
        else if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var select)) ((SelectionItemPattern)select).Select();
        else if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle))
        {
            if (((TogglePattern)toggle).Current.ToggleState != ToggleState.On) ((TogglePattern)toggle).Toggle();
        }
        else throw new InvalidOperationException("This control does not expose an activation action.");
    }

    internal static AutomationElement? FindVisible(AutomationElement root, ControlType type, Func<string, bool> matches)
    {
        var elements = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
        var found = new List<AutomationElement>();
        foreach (AutomationElement element in elements)
        {
            try { if (!element.Current.IsOffscreen && element.Current.IsEnabled && matches(element.Current.Name)) found.Add(element); }
            catch (ElementNotAvailableException) { }
        }
        if (found.Count > 1) throw new InvalidOperationException("The active control is ambiguous.");
        return found.SingleOrDefault();
    }

    internal static T WaitFor<T>(Func<T?> read) where T : class
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 2500)
        {
            T? value = read();
            if (value != null) return value;
            Thread.Sleep(50);
        }
        throw new TimeoutException("Codex did not expose the expected model control.");
    }

    private static void RequireForeground(nint window)
    {
        if (!IsCodexWindow(window) || GetForegroundWindow() != window) throw new InvalidOperationException("Focus Codex before selecting a preset.");
    }

    internal static bool IsForeground(nint window) => IsCodexWindow(window) && GetForegroundWindow() == window;

    internal static void FocusApp()
    {
        nint window = FindWindow();
        if (IsIconic(window)) ShowWindow(window, 9);
        if (!SetForegroundWindow(window) && !IsForeground(window)) throw new InvalidOperationException("Focus Codex and try again.");
    }

    internal static void CloseControls()
    {
        nint window = FindWindow();
        RequireForeground(window);
        var root = AutomationElement.FromHandle(window);
        if (FindVisible(root, ControlType.MenuItem, name => name == "Power" || name == "Select model") != null) PressKey(0x1B);
    }

    internal static void OpenModelList()
    {
        OpenControls();
        var root = AutomationElement.FromHandle(FindWindow());
        Activate(WaitFor(() => FindVisible(root, ControlType.MenuItem, name => name == "Select model")));
        Thread.Sleep(250);
    }

    internal static string[] ListAvailableModels()
    {
        nint window = FindWindow();
        FocusApp();
        var root = AutomationElement.FromHandle(window);
        try
        {
            OpenModelList();
            var buttons = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton));
            var names = buttons.Cast<AutomationElement>().Where(button =>
            {
                try { return !button.Current.IsOffscreen && button.Current.IsEnabled && button.Current.Name.StartsWith("GPT-", StringComparison.Ordinal); }
                catch (ElementNotAvailableException) { return false; }
            }).Select(button => button.Current.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (names.Length == 0) throw new InvalidOperationException("Codex did not expose model choices.");
            return names;
        }
        finally { if (IsForeground(window)) PressKey(0x1B); }
    }

    internal static object InspectPower()
    {
        OpenControls();
        var root = AutomationElement.FromHandle(FindWindow());
        var power = WaitFor(() => FindVisible(root, ControlType.MenuItem, name => name == "Power"));
        var nodes = power.FindAll(TreeScope.Subtree, Condition.TrueCondition);
        return nodes.Cast<AutomationElement>().Select(node => new { node.Current.Name,
            type = node.Current.ControlType.ProgrammaticName, rectangle = node.Current.BoundingRectangle.ToString(),
            patterns = node.GetSupportedPatterns().Select(pattern => pattern.ProgrammaticName).ToArray() }).ToArray();
    }

    private static void PressKey(ushort key)
    {
        System.Windows.Forms.SendKeys.SendWait(key switch { 0x25 => "{LEFT}", 0x27 => "{RIGHT}", 0x23 => "{END}", 0x1B => "{ESC}", _ => throw new ArgumentOutOfRangeException(nameof(key)) });
    }

    internal static bool EffortMatches(string expected, string actual) => expected switch
    {
        "Low" => actual is "Low" or "Light",
        "High" => actual is "High" or "Extended",
        "Extra High" => actual == "Extra High",
        _ => false
    };

    private static void ClickPoint(nint window, int x, int y, AutomationElement expected, AutomationElement? alternative = null)
    {
        RequireForeground(window);
        GetCursorPos(out var original);
        try
        {
            if (!SetCursorPos(x, y)) throw new InvalidOperationException("Windows could not point at the reasoning slider.");
            if (!IsCodexWindow(GetAncestor(WindowFromPoint(new Point { X = x, Y = y }), 2))) throw new InvalidOperationException("Another window is covering the model controls.");
            var hit = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            bool matches = false;
            for (int depth = 0; hit != null && depth < 12; depth++, hit = TreeWalker.ControlViewWalker.GetParent(hit))
            {
                if (Automation.Compare(hit, expected) || alternative != null && Automation.Compare(hit, alternative)) { matches = true; break; }
            }
            if (!matches) throw new InvalidOperationException("The expected control is no longer under the pointer.");
            Input[] events = [new() { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 2 } } },
                new() { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 4 } } }];
            if (SendInput((uint)events.Length, events, Marshal.SizeOf<Input>()) != events.Length)
                throw new InvalidOperationException("Windows did not accept the reasoning selection.");
        }
        finally { SetCursorPos(original.X, original.Y); }
    }

    internal static void ClickElement(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var scroll)) ((ScrollItemPattern)scroll).ScrollIntoView();
        var rectangle = element.Current.BoundingRectangle;
        if (element.Current.IsOffscreen || rectangle.Width <= 0 || rectangle.Height <= 0) throw new InvalidOperationException("The target control is not visible.");
        ClickPoint(FindWindow(), (int)Math.Round(rectangle.Left + rectangle.Width / 2), (int)Math.Round(rectangle.Top + rectangle.Height / 2), element);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    internal static nint FindWindow()
    {
        nint foreground = GetForegroundWindow();
        if (IsCodexWindow(foreground)) return foreground;
        var matches = new List<nint>();
        EnumWindows((window, _) => {
            if (IsWindowVisible(window) && GetWindow(window, 4) == 0 && IsCodexWindow(window)) matches.Add(window);
            return true;
        }, 0);
        if (matches.Count != 1) throw new InvalidOperationException($"Expected one Codex window; found {matches.Count}.");
        return matches[0];
    }

    internal static bool IsCodexWindow(nint window)
    {
        if (window == 0) return false;
        GetWindowThreadProcessId(window, out uint pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            string? path = process.MainModule?.FileName;
            return path != null && path.Contains(@"\WindowsApps\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(path).Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    internal static object Inspect()
    {
        nint window = FindWindow();
        var root = AutomationElement.FromHandle(window);
        var elements = root.FindAll(TreeScope.Descendants, new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)));
        var result = new List<object>();
        foreach (AutomationElement element in elements)
        {
            var info = element.Current;
            if (!Regex.IsMatch(info.Name, @"^(GPT-|Select effort$|Select model$|Power$|Default(?: |$)|New chat$|Open your dot$|Increase reasoning effort$|Decrease reasoning effort$|Fork chat$|Send message$|Push to talk$)")) continue;
            result.Add(new { info.Name, type = info.ControlType.ProgrammaticName, info.IsOffscreen, info.IsEnabled, info.IsKeyboardFocusable,
                patterns = element.GetSupportedPatterns().Select(p => p.ProgrammaticName).ToArray() });
        }
        var documents = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document))
            .Cast<AutomationElement>().Select(element => new { element.Current.Name, element.Current.IsOffscreen, bounds = element.Current.BoundingRectangle.ToString() }).ToArray();
        return new { window = window.ToInt64(), foreground = GetForegroundWindow() == window, bounds = root.Current.BoundingRectangle.ToString(), documents, elements = result };
    }

    private delegate bool EnumWindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
}
