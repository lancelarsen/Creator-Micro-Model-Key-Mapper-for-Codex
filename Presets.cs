namespace MicroPresets;

using System.IO;

internal sealed record Preset(string Position, string HardwareKey, string Model, string Effort, int EffortPosition);

internal static class Presets
{
    private static readonly string[] Positions = ["H", "I", "J"];
    private static readonly string[] HardwareKeys = ["ACT07", "ACT08", "ACT09"];

    internal static readonly string[] KnownModels =
    [
        "GPT-6.1 Sol", "GPT-6 Astra", "GPT-6 Sol", "GPT-6 Luna",
        "GPT-5.6 Sol", "GPT-5.6 Terra", "GPT-5.6 Luna"
    ];

    private static Preset[] all = Defaults();
    internal static Preset[] All => Volatile.Read(ref all);

    internal static Preset[] Defaults() =>
    [
        new("H", "ACT07", "GPT-6 Luna", "Extra High", 4),
        new("I", "ACT08", "GPT-6.1 Sol", "High", 3),
        new("J", "ACT09", "GPT-6 Astra", "Low", 1)
    ];

    internal static Preset[] WithAssignments(IEnumerable<Preset> presets)
    {
        var assigned = presets.OrderBy(preset => Array.IndexOf(Positions, preset.Position)).ToArray();
        if (assigned.Length != Positions.Length || assigned.Where((preset, index) =>
                preset.Position != Positions[index] || preset.HardwareKey != HardwareKeys[index] ||
                string.IsNullOrWhiteSpace(preset.Model) || preset.EffortPosition is < 1 or > 5).Any())
            throw new InvalidDataException("Preset settings must include one valid assignment for H, I, and J.");
        return assigned;
    }

    internal static void SetAll(IEnumerable<Preset> presets) => Volatile.Write(ref all, WithAssignments(presets));
}
