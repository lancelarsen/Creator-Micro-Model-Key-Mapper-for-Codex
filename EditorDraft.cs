namespace MicroPresets;

internal sealed class EditorDraft
{
    internal Preset[] Original { get; private set; } = Presets.Defaults();
    internal Preset[] Assignments { get; private set; } = Presets.Defaults();
    internal bool RefreshOnOpen { get; set; }
    private bool originalRefresh;
    internal bool IsDirty => !Original.SequenceEqual(Assignments) || RefreshOnOpen != originalRefresh;
    internal void Load(EditorReply reply)
    {
        Original = Presets.WithAssignments(reply.Assignments ?? throw new InvalidOperationException("Assignments are missing."));
        Assignments = Original.ToArray();
        RefreshOnOpen = originalRefresh = reply.RefreshOnOpen;
    }
    internal void Discard() { Assignments = Original.ToArray(); RefreshOnOpen = originalRefresh; }
    internal void SetRefreshPreference(bool value) { RefreshOnOpen = originalRefresh = value; }
    internal EditorRequest SaveRequest() => new("save", Original, Presets.WithAssignments(Assignments), RefreshOnOpen);
}
