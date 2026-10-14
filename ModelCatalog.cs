using System.IO;
using System.Text.Json;

namespace MicroPresets;

internal sealed record ModelCatalog(string[] Models, DateTimeOffset? CheckedAt, bool RefreshOnOpen)
{
    private static string CatalogPath => Path.Combine(PresetStore.DataDirectory, "models.json");
    internal static ModelCatalog Load()
    {
        try
        {
            var catalog = JsonSerializer.Deserialize<ModelCatalog>(File.ReadAllText(CatalogPath));
            if (catalog != null && catalog.Models is { Length: > 1 } && catalog.Models.All(m => !string.IsNullOrWhiteSpace(m))) return catalog;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new(Presets.KnownModels, null, true);
    }
    internal ModelCatalog WithRefresh(string[] models)
    {
        if (models.Length < 2 || models.Length < Models.Length)
            throw new InvalidOperationException("Codex returned fewer models than the saved list. The previous list has been kept; reopen Codex's model menu and refresh again.");
        return this with { Models = models, CheckedAt = DateTimeOffset.Now };
    }
    internal void Save()
    {
        Directory.CreateDirectory(PresetStore.DataDirectory);
        File.WriteAllText(CatalogPath + ".tmp", JsonSerializer.Serialize(this));
        File.Move(CatalogPath + ".tmp", CatalogPath, true);
    }
}

internal static class StaWork
{
    internal static Task<T> Run<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(action()); }
            catch (Exception e) { completion.SetException(e); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}

// Serialize Codex UI operations across the tray app and short-lived selector processes.
internal sealed class CodexOperation : IDisposable
{
    private readonly Mutex mutex = new(false, "Local\\CreatorMicroModelKeys.CodexUI");
    internal CodexOperation()
    {
        bool acquired;
        try { acquired = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) { mutex.Dispose(); throw new InvalidOperationException("Another Codex key operation is running. Try again when it finishes."); }
    }
    public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
}
