using System.Text;

namespace MicroPresets;

internal static class SelfTests
{
    public static void Run()
    {
        int passed = 0;
        void Check(string name, Func<bool> test)
        {
            if (!test()) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }
        string Notify(string key, int act) => "{\"m\":\"v.oai.hid\",\"p\":{\"k\":\"" + key + "\",\"act\":" + act + "}}\n";
        Check("physical H selects Luna Extra High", () => new ReportDecoder().Feed(1, Packet(Notify("ACT07", 1))).SingleOrDefault()?.Model == "GPT-6 Luna");
        Check("physical I selects Sol High", () => new ReportDecoder().Feed(1, Packet(Notify("ACT08", 1))).SingleOrDefault()?.Effort == "High");
        Check("physical J selects Astra Low", () => new ReportDecoder().Feed(1, Packet(Notify("ACT09", 1))).SingleOrDefault()?.Effort == "Low");
        Check("preserve all other physical keys", () => new[] { "AG00", "AG01", "AG02", "AG03", "AG04", "AG05", "ACT06", "ACT10", "ACT11", "ACT12", "ENC_CW" }
            .All(key => new ReportDecoder().Feed(1, Packet(Notify(key, 1))).Count == 0));
        Check("ignore release and held-key repeats", () => new[] { 0, 2 }.All(act => new ReportDecoder().Feed(1, Packet(Notify("ACT07", act))).Count == 0));
        Check("ignore RPC responses and other notifications", () => new ReportDecoder().Feed(1, Packet("{\"i\":5,\"m\":\"v.oai.hid\",\"p\":{\"k\":\"ACT07\",\"act\":1}}\n")).Count == 0
            && new ReportDecoder().Feed(1, Packet("{\"m\":\"v.oai.rad\",\"p\":{\"k\":\"ACT07\",\"act\":1}}\n")).Count == 0);
        Check("reassemble split HID frames", () => {
            var decoder = new ReportDecoder();
            string json = Notify("ACT07", 1);
            return decoder.Feed(1, Packet(json[..25])).Count == 0 && decoder.Feed(1, Packet(json[25..])).SingleOrDefault()?.Position == "H";
        });
        Check("device fragments remain separate", () => {
            var decoder = new ReportDecoder();
            string json = Notify("ACT07", 1);
            decoder.Feed(1, Packet(json[..25]));
            return decoder.Feed(2, Packet(json[25..])).Count == 0 && decoder.Feed(1, Packet(json[25..])).Count == 1;
        });
        Check("handle multiple notifications in one report", () => new ReportDecoder().Feed(1, Packet(Notify("ACT07", 1) + Notify("ACT08", 1))).Count == 2);
        Check("reject malformed reports and recover", () => {
            var decoder = new ReportDecoder();
            return decoder.Feed(1, [6, 2, 60, 1]).Count == 0 && decoder.Feed(1, Packet("invalid\n")).Count == 0
                && decoder.Feed(1, Packet(Notify("ACT09", 1))).Count == 1;
        });
        Check("ignore non-RPC channels", () => new ReportDecoder().Feed(1, Packet(Notify("ACT07", 1), 1)).Count == 0);
        Check("ignore ordinary typed H I J", () => new[] { "H", "I", "J" }.All(key => new ReportDecoder().Feed(1, Packet(Notify(key, 1))).Count == 0));
        Check("read semantic reasoning position from accessible status", () => ModelSelector.ParsePower("GPT-6.1 Sol Extended, 3 of 6.", "GPT-6.1 Sol")?.Position == 3);
        Check("reject status from another model", () => ModelSelector.ParsePower("GPT-6 Sol Extended, 3 of 6.", "GPT-6.1 Sol") == null);
        Check("reject invalid reasoning positions", () => ModelSelector.ParsePower("GPT-6 Luna Extra High, 8 of 5.", "GPT-6 Luna") == null);
        Check("read extra-high label and model exactly", () => ModelSelector.ParsePower("GPT-6 Luna Extra High, 4 of 5.", "GPT-6 Luna")?.Label == "Extra High");
        Check("recognize Low and High display aliases", () => ModelSelector.EffortMatches("Low", "Light") && ModelSelector.EffortMatches("High", "Extended"));
        Check("reject unexpected reasoning labels", () => !ModelSelector.EffortMatches("High", "Medium") && !ModelSelector.EffortMatches("Extra High", "Max"));
        Check("migrate existing H I J assignments without enabling other keys", () => {
            var legacy = Presets.Defaults().Where(p => p.Enabled).ToArray();
            var migrated = Presets.MigrateLegacy(legacy);
            return migrated.Length == 7 && migrated.Count(p => p.Enabled) == 3 && migrated.Single(p => p.Position == "H") == legacy[0];
        });
        Check("each of the seven lower keys can override independently", () => {
            try {
                foreach (var target in Presets.Defaults()) {
                    Presets.SetAll(Presets.Defaults().Select(p => p with { Enabled = p.Position == target.Position }));
                    foreach (var key in Presets.All)
                        if (new ReportDecoder().Feed(1, Packet(Notify(key.HardwareKey, 1))).Count != (key.Position == target.Position ? 1 : 0)) return false;
                }
                return true;
            } finally { Presets.SetAll(Presets.Defaults()); }
        });
        Check("default mode passes all lower keys through", () => {
            try {
                Presets.SetAll(Presets.Defaults().Select(p => p with { Enabled = false }));
                return Presets.All.All(p => new ReportDecoder().Feed(1, Packet(Notify(p.HardwareKey, 1))).Count == 0);
            } finally { Presets.SetAll(Presets.Defaults()); }
        });
        Check("reject duplicate or remapped hardware keys", () => {
            try { Presets.WithAssignments(Presets.Defaults().Select(p => p.Position == "G" ? p with { HardwareKey = "AG00" } : p)); return false; }
            catch (System.IO.InvalidDataException) { return true; }
        });
        Console.WriteLine($"{passed} tests passed.");
    }

    internal static byte[] Packet(string payload, byte channel = 2)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        if (bytes.Length > 255) throw new ArgumentException("Fixture too large");
        return [6, channel, (byte)bytes.Length, ..bytes];
    }
}
