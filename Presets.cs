namespace MicroPresets;

using System.IO;

internal sealed record Preset(string Position, string HardwareKey, string Model, string Effort, int EffortPosition, bool Enabled = true);

internal static class Presets
{
    internal static readonly string[] Positions = ["G", "H", "I", "J", "K", "L", "M"];
    internal static readonly string[] DefaultActions = ["New chat", "Decrease reasoning", "Increase reasoning", "Fork chat", "Dictation", "Send message", "Your dot"];
    private static readonly string[] HardwareKeys = ["ACT06", "ACT07", "ACT08", "ACT09", "ACT10", "ACT11", "ACT12"];
    internal static readonly string[] EffortNames = ["Low", "Medium", "High", "Extra High", "Maximum", "Ultra"];

    internal static readonly string[] KnownModels =
    [
        "GPT-6.1 Sol", "GPT-6 Astra", "GPT-6 Sol", "GPT-6 Luna",
        "GPT-5.6 Sol", "GPT-5.6 Terra", "GPT-5.6 Luna"
    ];

    private static Preset[] all = Defaults();
    internal static Preset[] All => Volatile.Read(ref all);

    internal static Preset[] Defaults() =>
    [
        new("G", "ACT06", "GPT-6.1 Sol", "High", 3, false),
        new("H", "ACT07", "GPT-6 Luna", "Extra High", 4),
        new("I", "ACT08", "GPT-6.1 Sol", "High", 3),
        new("J", "ACT09", "GPT-6 Astra", "Low", 1),
        new("K", "ACT10", "GPT-6.1 Sol", "High", 3, false),
        new("L", "ACT11", "GPT-6.1 Sol", "High", 3, false),
        new("M", "ACT12", "GPT-6.1 Sol", "High", 3, false)
    ];

    internal static Preset[] WithAssignments(IEnumerable<Preset> presets)
    {
        var assigned = presets.OrderBy(preset => Array.IndexOf(Positions, preset.Position)).ToArray();
        if (assigned.Length != Positions.Length || assigned.Where((preset, index) =>
                preset.Position != Positions[index] || preset.HardwareKey != HardwareKeys[index] ||
                string.IsNullOrWhiteSpace(preset.Model) || preset.Model.Length > 100 || preset.EffortPosition is < 1 or > 6).Any())
            throw new InvalidDataException("Settings must include one valid assignment for each key from G through M.");
        return assigned.Select(p => p with { Effort = EffortNames[p.EffortPosition - 1] }).ToArray();
    }

    internal static Preset[] MigrateLegacy(Preset[] presets)
    {
        if (presets.Length != 3 || !presets.Select(p => p.Position).Order().SequenceEqual(new[] { "H", "I", "J" }))
            throw new InvalidDataException("The legacy assignments are invalid.");
        return WithAssignments(Defaults().Select(p => presets.SingleOrDefault(old => old.Position == p.Position) ?? p));
    }

    internal static void SetAll(IEnumerable<Preset> presets) => Volatile.Write(ref all, WithAssignments(presets));
}
