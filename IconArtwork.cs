using System.Drawing;
using System.IO;

namespace MicroPresets;

internal static class IconArtwork
{
    internal static Icon Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "codex-micro-presets.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }
}
