using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MicroPresets;

// Read-only raw input subscription. Never opens or writes to the device,
// intercepts ordinary keystrokes, or changes Codex's lighting connection.
internal sealed class MicroInputListener : NativeWindow, IDisposable
{
    private const ushort VendorPage = 0xFF00;
    private const uint InputSink = 0x100, PageOnly = 0x20, DeviceNotify = 0x2000;
    private const uint RidInput = 0x10000003, RidDeviceInfo = 0x2000000B;
    private readonly Dictionary<nint, bool> deviceCache = new();
    private readonly Action<nint, byte[]> onReport;
    private readonly Action<object> log;

    public MicroInputListener(Action<nint, byte[]> onReport, Action<object> log)
    {
        this.onReport = onReport;
        this.log = log;
        CreateHandle(new CreateParams { Caption = "Codex Micro Presets input", Parent = new nint(-3) });
        var registration = new RawInputDevice { UsagePage = VendorPage, Usage = 0, Flags = InputSink | PageOnly | DeviceNotify, Target = Handle };
        if (!RegisterRawInputDevices([registration], 1, (uint)Marshal.SizeOf<RawInputDevice>()))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot register the Micro input listener");
        log(new { kind = "listener-ready", usagePage = VendorPage });
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x00FE) deviceCache.Remove(message.LParam); // WM_INPUT_DEVICE_CHANGE
        if (message.Msg == 0x00FF) // WM_INPUT
        {
            try { ReadInput(message.LParam); }
            catch (Exception error) { log(new { kind = "input-error", message = error.Message }); }
        }
        base.WndProc(ref message);
    }

    private void ReadInput(nint handle)
    {
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>(), size = 0;
        if (GetRawInputData(handle, RidInput, null, ref size, headerSize) == uint.MaxValue || size < headerSize + 8 || size > 65536) return;
        byte[] data = new byte[size];
        if (GetRawInputData(handle, RidInput, data, ref size, headerSize) == uint.MaxValue) return;
        if (BitConverter.ToUInt32(data, 0) != 2) return; // RIM_TYPEHID
        nint device = IntPtr.Size == 8 ? new nint(BitConverter.ToInt64(data, 8)) : new nint(BitConverter.ToInt32(data, 8));
        if (!IsMicro(device)) return;
        int start = (int)headerSize + 8;
        uint reportSize = BitConverter.ToUInt32(data, (int)headerSize), count = BitConverter.ToUInt32(data, (int)headerSize + 4);
        if (reportSize == 0 || reportSize > 4096 || count > 64 || start + (long)reportSize * count > data.Length) return;
        for (int i = 0; i < count; i++) onReport(device, data.AsSpan(start + i * (int)reportSize, (int)reportSize).ToArray());
    }

    private bool IsMicro(nint device)
    {
        if (deviceCache.TryGetValue(device, out bool known)) return known;
        uint size = 32;
        byte[] info = new byte[size];
        BitConverter.GetBytes(size).CopyTo(info, 0);
        bool matches = GetRawInputDeviceInfoW(device, RidDeviceInfo, info, ref size) != uint.MaxValue
            && BitConverter.ToUInt32(info, 4) == 2
            && BitConverter.ToUInt32(info, 8) == 12346
            && BitConverter.ToUInt32(info, 12) is 33632 or 33431 or 33432
            && BitConverter.ToUInt16(info, 20) == VendorPage;
        deviceCache[device] = matches;
        if (matches) log(new { kind = "micro-detected", device = device.ToInt64(), product = BitConverter.ToUInt32(info, 12), usage = BitConverter.ToUInt16(info, 22) });
        return matches;
    }

    public void Dispose()
    {
        if (Handle == 0) return;
        var removal = new RawInputDevice { UsagePage = VendorPage, Flags = 0x01 | PageOnly };
        RegisterRawInputDevices([removal], 1, (uint)Marshal.SizeOf<RawInputDevice>());
        DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice { public ushort UsagePage, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader { public uint Type, Size; public nint Device, WParam; }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(nint input, uint command, byte[]? data, ref uint size, uint headerSize);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfoW(nint device, uint command, byte[] data, ref uint size);
}
