using System.IO;
using System.IO.Pipes;
using System.Text.Json;

namespace MicroPresets;

internal sealed class EditorServer : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly Func<EditorRequest, Task<EditorReply>> handle;
    private readonly string pipeName;
    internal EditorServer(Func<EditorRequest, Task<EditorReply>> handle, string? pipeName = null) { this.handle = handle; this.pipeName = pipeName ?? EditorProtocol.PipeName; _ = Serve(); }
    private async Task Serve()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop.Token);
                using var reader = new StreamReader(pipe);
                using var writer = new StreamWriter(pipe) { AutoFlush = true };
                EditorReply reply;
                try
                {
                    using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    readTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                    var line = await ReadRequest(reader, readTimeout.Token);
                    var request = JsonSerializer.Deserialize<EditorRequest>(line) ?? throw new InvalidDataException("Empty settings request.");
                    reply = await handle(request);
                }
                catch (Exception e) { reply = new(false, e.Message); }
                await writer.WriteLineAsync(JsonSerializer.Serialize(reply));
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (IOException) { await Task.Delay(100); }
        }
    }
    private static async Task<string> ReadRequest(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[1];
        var text = new System.Text.StringBuilder();
        while (text.Length <= 32768)
        {
            if (await reader.ReadAsync(buffer.AsMemory(), token) == 0) throw new IOException("Editor disconnected.");
            if (buffer[0] == '\n') return text.ToString();
            text.Append(buffer[0]);
        }
        throw new InvalidDataException("Settings request is too large.");
    }
    public void Dispose() { stop.Cancel(); }
}
