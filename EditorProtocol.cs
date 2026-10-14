using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;

namespace MicroPresets;

internal sealed record EditorRequest(string Operation, Preset[]? Original = null, Preset[]? Assignments = null, bool RefreshOnOpen = false);
internal sealed record EditorReply(bool Success, string? Error = null, Preset[]? Assignments = null, string[]? Models = null, DateTimeOffset? CheckedAt = null, bool RefreshOnOpen = false);

internal static class EditorProtocol
{
    internal static string PipeName => "CreatorMicroModelKeys.Editor." + WindowsIdentity.GetCurrent().User!.Value + "." + System.Diagnostics.Process.GetCurrentProcess().SessionId;
    internal static async Task<EditorReply> Request(EditorRequest request, string? pipeName = null)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName ?? PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await pipe.ConnectAsync(3000, timeout.Token);
        using var reader = new StreamReader(pipe);
        using var writer = new StreamWriter(pipe) { AutoFlush = true };
        await writer.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), timeout.Token);
        var line = await reader.ReadLineAsync(timeout.Token) ?? throw new IOException("The helper disconnected. Reopen settings to check the saved assignments.");
        var reply = JsonSerializer.Deserialize<EditorReply>(line) ?? throw new IOException("The helper returned an invalid response.");
        if (!reply.Success) throw new InvalidOperationException(reply.Error);
        return reply;
    }
}
