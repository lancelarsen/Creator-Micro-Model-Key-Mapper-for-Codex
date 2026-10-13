using System.Text;
using System.Text.Json;

namespace MicroPresets;

internal sealed class ReportDecoder
{
    private readonly Dictionary<nint, StringBuilder> buffers = new();

    public IReadOnlyList<Preset> Feed(nint device, byte[] report)
    {
        var result = new List<Preset>();
        if (report.Length < 3 || report[0] != 6 || report[1] != 2 || report[2] > report.Length - 3) return result;
        if (!buffers.TryGetValue(device, out var buffer))
        {
            if (buffers.Count >= 8) buffers.Clear();
            buffers[device] = buffer = new StringBuilder();
        }
        buffer.Append(Encoding.UTF8.GetString(report, 3, report[2]));
        if (buffer.Length > 8192) { buffer.Clear(); return result; }
        string contents = buffer.ToString();
        int consumed = 0;
        while (contents.IndexOf('\n', consumed) is int end && end >= 0)
        {
            string line = contents[consumed..end].Trim();
            consumed = end + 1;
            var preset = Parse(line);
            if (preset != null) result.Add(preset);
        }
        if (consumed > 0) buffer.Remove(0, consumed);
        return result;
    }

    private static Preset? Parse(string line)
    {
        try
        {
            using var json = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 8 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("id", out _) || root.TryGetProperty("i", out _)) return null;
            if (!(root.TryGetProperty("m", out var method) || root.TryGetProperty("method", out method)) || method.ValueKind != JsonValueKind.String || method.GetString() != "v.oai.hid") return null;
            if (!(root.TryGetProperty("p", out var parameters) || root.TryGetProperty("params", out parameters)) || parameters.ValueKind != JsonValueKind.Object) return null;
            if (!parameters.TryGetProperty("act", out var action) || !action.TryGetInt32(out int value) || value != 1) return null;
            if (!parameters.TryGetProperty("k", out var key) || key.ValueKind != JsonValueKind.String) return null;
            return Presets.All.FirstOrDefault(preset => preset.HardwareKey == key.GetString());
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }
}
