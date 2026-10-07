using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AquaMai.ChartVisuals;
using MuConvert.mai;
using Rationals;

// 无旁车文件的既有 MA2：以游戏的时轴为准，恢复独立流、扩展音符及特效表。
internal sealed class Ma2Preview
{
    readonly List<string[]> rows;
    readonly List<string> headers;
    readonly MaiChart clock;
    readonly int resolution;
    static string F(double value) => value.ToString("0.#########", CultureInfo.InvariantCulture);
    Rational Time(string[] row) => int.Parse(row[1]) + new Rational(int.Parse(row[2]), resolution);
    double Seconds(string[] row) => (double)clock.ToSecond(Time(row));
    string Scope(string[] row) => row.LastOrDefault(v => Regex.IsMatch(v, @"^s[0-9]+$")) ?? "";
    static readonly HashSet<string> Curves = new(StringComparer.OrdinalIgnoreCase) { "SVSP", "HS", "SPAWN", "SPAWNMODE", "DESTROY", "BOUNCE", "COLORV", "SIZEV", "ALPHAV" };
    bool IsCommand(string[] row) => row.Length == 4 && (Curves.Contains(row[0]) || row[0] is "TEXT" or "NZONE" || PresentationCommands.IsKind(row[0].ToLowerInvariant()) || MediaCommands.IsKind(row[0].ToLowerInvariant()));
    public Ma2Preview(string text)
    {
        var all = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        headers = all.Where(l => Regex.IsMatch(l, @"^(VERSION|FES_MODE|BPM_DEF|MET_DEF|RESOLUTION|CLK_DEF|COMPATIBLE_CODE|GENERATED_BY|BPM|MET|CLK)\t")).ToList();
        resolution = int.Parse(headers.FirstOrDefault(l => l.StartsWith("RESOLUTION\t"))?.Split('\t')[1] ?? "384");
        rows = all.Where(l => !headers.Contains(l) && !Regex.IsMatch(l, @"^(T_REC|T_NUM|T_JUDGE|TTM_|SEF)")).Select(l => l.Split('\t')).ToList();
        clock = new MA2Parser().Parse(string.Join('\n', headers) + "\nNMTAP\t0\t0\t0").Item1;
        clock.Notes.Clear();
    }
    public string Generate()
    {
        string Group(string[] row) => Scope(row) + "\t" + (row.FirstOrDefault(v=>v.StartsWith("VS|")) ?? "") + "\t" + row.Contains("FK");
        var groups = rows.Where(r => !IsCommand(r)).Select(Group).Prepend("\t\tFalse").Distinct().ToList();
        var output = new StringBuilder();
        foreach (var group in groups)
        {
            var parts = group.Split('\t'); var scope=parts[0];
            var groupVisual=parts[1].Length==0?null:VisualNote.Decode(parts[1]);
            var noteRows = rows.Where(r => !IsCommand(r) && Group(r) == group).ToList();
            var custom = noteRows.Where(r => r[0].EndsWith("SSS")).ToList();
            var regular = noteRows.Except(custom).ToList();
            var body = string.Join('\n', regular.Select(r => string.Join('\t', r)));
            var chart = new MA2Parser().Parse(string.Join('\n', headers) + "\n" + (regular.Count == 0 ? "NMTAP\t0\t0\t0" : body)).Item1;
            if (regular.Count == 0) chart.Notes.Clear();
            // c 已经烘焙成 IgnoreSV；解析器版本不接受它时，用本独立流的单位 SV 表达。
            foreach(var note in chart.Notes) {
                note.IgnoreSV=false;
                if(note is Slide slide) {
                    if(slide.OwnHead!=null) slide.OwnHead.IgnoreSV=false;
                    foreach(var seg in slide.segments) if(seg.Duration != null && seg.Duration.Bar==0) seg.Duration.Bar=new Rational(1,3840);
                }
            }
            var extraHeads = chart.Notes.OfType<Slide>().Where(s=>s.OwnHead is not null and not Star).ToList(); foreach(var slide in extraHeads) { chart.Notes.Add(slide.OwnHead!); slide.OwnHead=null; }
            var generator = new SimaiGenerator();
            foreach (var row in custom)
            {
                var visual = row.FirstOrDefault(v => v.StartsWith("VS|"));
                var snapshot = visual == null ? null : VisualNote.Decode(visual);
                var route = snapshot?.ReferenceSlide ?? row[7];
                if (route.StartsWith("TG1:") || route.StartsWith("DG1:") || route.StartsWith("DF1:")) route = route[4..];
                // 所有星头已经是独立 MA2 记录；滑条体不能再生成一个头。
                var start = Regex.Match(route, @"^(?:[1-8]d?|[A-E][1-8]?)");
                if (!start.Success) throw new InvalidDataException("Could not restore SSS path: " + route);
                route = route.Insert(start.Length, "?");
                var time = Time(row);
                var wait = new Rational(int.Parse(row[4]), resolution);
                var duration = new Rational(int.Parse(row[5]), resolution);
                var waitSeconds = (double)(clock.ToSecond(time + wait) - clock.ToSecond(time));
                var durationSeconds = (double)(clock.ToSecond(time + wait + duration) - clock.ToSecond(time + wait));
                var modifiers = (row[0].StartsWith("MN") || row[0].StartsWith("MB") ? "m" : "") + (row[0].StartsWith("BR") || row[0].StartsWith("MB") ? "b" : "");
                generator.AdditionalExpressions.Add((time, route + "[" + F(waitSeconds) + "##" + F(Math.Max(.0001,durationSeconds)) + "]" + modifiers, false));
            }
            foreach (var row in rows.Where(IsCommand))
            {
                if (Curves.Contains(row[0]))
                {
                    if(row[0]=="SVSP" && groupVisual?.IgnoreSV==true) continue;
                    var value = row[3];
                    var scoped = Regex.Match(value, @"^(s[0-9]+)[=~](.*)$");
                    if ((scoped.Success ? scoped.Groups[1].Value : "") != scope) continue;
                    if (scoped.Success) value = scoped.Groups[2].Value;
                    var kind = row[0] == "SVSP" ? "SV" : row[0];
                    generator.AdditionalExpressions.Add((Time(row), "<" + kind + "*" + value + ">", true));
                }
                else if (group == "\t\tFalse" && row[0] == "NZONE")
                {
                    if (!NoiseZoneSpec.TryDecode(row[3], out var noise)) throw new InvalidDataException("Invalid NZONE");
                    var id = noise.Sensor;
                    var area = id < 8 ? "A" + (id + 1) : id < 16 ? "B" + (id - 7) : id == 16 ? "C" : id < 25 ? "D" + (id - 16) : "E" + (id - 24);
                    generator.AdditionalExpressions.Add((Time(row), "X" + area + "[120#" + F(Math.Max(.0001,noise.Duration)) + "]", false));
                }
            }
            var inote = Visual(groupVisual,parts[2]=="True") + generator.Generate(chart).Item1;
            inote = Regex.Replace(inote, @",\s*E\s*$", ",");
            if (group == "\t\tFalse") output.Insert(0, inote + "\n");
            else output.Append("@*{4}").Append(inote.Replace("\r", "").Replace("\n", "")).Append("*@\n");
        }
        // 独立流须从全曲零点发出，不能接在主谱末尾。
        var source = output.ToString();
        var streams = Regex.Matches(source, @"@\*.*?\*@").Select(m => m.Value).ToArray();
        source = Regex.Replace(source, @"@\*.*?\*@", "");
        return "(" + F((double)clock.BpmList[0].Bpm) + "){4}" + string.Join('\n', streams) + "\n" + source + "E";
    }
    static string Visual(VisualNote? visual, bool fake)
    {
        var value = new StringBuilder(fake ? "<FAKE*True>" : "<FAKE*False>");
        value.Append("<COLOR*NULL><SIZE*NULL><ALPHA*NULL>");
        void Add(VisualValue v, string family)
        {
            if (v.Color.HasValue) value.Append("<COLOR*").Append(family).Append("=#").Append(v.Color.Value.ToString("X6")).Append('>');
            if (v.X.HasValue) value.Append("<SIZE*").Append(family).Append("=(").Append(F(v.X.Value)).Append(',').Append(F(v.Y!.Value)).Append(")>");
            if (v.Alpha.HasValue) value.Append("<ALPHA*").Append(family).Append('=').Append(F(v.Alpha.Value)).Append('>');
        }
        if (visual != null) { Add(visual.Base, visual.Family); Add(visual.Guide, "slidestar"); }
        return value.ToString();
    }
    public string ApplyTables(string json, int defaultCombo = 1, float defaultBrightness = .4f)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        void Add(string table, object item) { ((JsonArray)root[table]!).Add(System.Text.Json.JsonSerializer.SerializeToNode(item)); }
        string Name(string kind) => kind switch {
            "tvnoise" => "TVNoise", "innerbrightness" => "InnerBrightness", "outerbrightness" => "OuterBrightness",
            "showjudgeinfo" => "ShowJudgeInfo", "showcomboinfo" => "ShowComboInfo", "showjudgetext" => "ShowJudgeText",
            "combodisplay" => "ComboDisplay", "showjudgeline" => "ShowJudgeLine", "judgeline" => "JudgeLine", "jline" => "JLine", "judgelineexpand" => "JudgeLineExpand",
            _ => ExtraScreenFilters.Find(kind)?.Uniform.TrimStart('_') ?? char.ToUpperInvariant(kind[0]) + kind[1..] };
        foreach (var row in rows.Where(IsCommand))
        {
            var kind = row[0].ToLowerInvariant(); var time = Seconds(row);
            if (kind is "showjudgeinfo" or "showcomboinfo" or "outerbrightness") continue;
            if (kind == "text")
            {
                if (!SubtitleCommands.Decode(row[3], time, out var c)) throw new InvalidDataException("Invalid TEXT");
                Add("subtitleTable", new { time, text=c.Text, duration=c.Duration, x=c.X, y=c.Y, size=c.Size, font=c.Font, index=c.Index, style=c.Style, transition=c.Transition });
            }
            else if (PresentationCommands.IsKind(kind))
            {
                if (!PresentationCommands.Decode(kind, row[3], time, out var c)) throw new InvalidDataException("Invalid " + row[0]);
                if (kind == "jline") Add("colorTable", new { time, noteType="judgeline", color=c.ColorHex, duration=c.Duration, streamIndex=0, sourcePosition=0, live=true });
                else if (ScreenEffectCommands.IsKind(kind) || kind is "shake" or "flash" or "tint" or "fade" or "jline" or "judgelineexpand")
                    Add("effectTable", new { time, effect=Name(kind), duration=c.Duration, intensity=c.Target, attack=c.ShakeMode==2?c.Attack:-1, holdTime=c.HoldTime, release=c.Release, paramA=kind=="shake"?c.Frequency:c.ParamA, paramB=kind=="shake"?c.Direction:c.ParamB, hasDirection=c.HasDirection, color=c.ColorHex, stateful=c.ShakeMode==0, enabled=c.ShakeEnabled, transition=c.Duration, gradient=c.Gradient });
                else Add("displayTable", new { time, property=Name(kind), target=c.Target<0?(kind=="innerbrightness"?defaultBrightness:kind=="combodisplay"?defaultCombo:1):c.Target, duration=c.Duration });
            }
            else if (MediaCommands.IsKind(kind))
            {
                if (!MediaCommands.Decode(kind, row[3], time, out var c)) throw new InvalidDataException("Invalid media");
                Add("mediaTable", new { time, c.kind, c.enabled, c.path, c.transition, c.track, c.sourceOffset, c.duration, c.timelineClip });
            }
        }
        foreach (var name in new[]{"subtitleTable","displayTable","effectTable","mediaTable"})
        {
            var entries = ((JsonArray)root[name]!).OrderBy(n=>n!["time"]!.GetValue<double>()).Select(n=>n!.DeepClone()).ToArray();
            root[name] = new JsonArray(entries);
        }
        return root.ToJsonString();
    }
}
