using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

// 编辑器解析器运行于短生命周期进程，避免把 WPF/Unity 留在 MCM 内存中。
try
{
    if (args.Length != 3) throw new ArgumentException("Usage: bridge <parser-dir> <source.json> <chart.json>");
    var parserDir = Path.GetFullPath(args[0]);
    AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
        var p = Path.Combine(parserDir, new AssemblyName(e.Name).Name + ".dll");
        return File.Exists(p) ? Assembly.LoadFrom(p) : null;
    };
    using var source = JsonDocument.Parse(File.ReadAllText(args[1]));
    var data = source.RootElement;
    var assembly = Assembly.LoadFrom(Path.Combine(parserDir, "MajdataEdit.dll"));
    var process = assembly.GetType("MajdataEdit.SimaiProcess", true)!;
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    string Render(bool useMa2)
    {
    process.GetMethod("ClearData")!.Invoke(null, null);
    foreach (var name in new[] { "title", "artist", "designer", "clockCount" })
        if (data.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out var v)) process.GetField(name, flags)!.SetValue(null, v.GetString() ?? "");
    process.GetField("first", flags)!.SetValue(null, useMa2 ? 0f : (float)data.GetProperty("OffsetSeconds").GetDouble());
    var fallback = useMa2 && data.TryGetProperty("Ma2", out var ma2) && ma2.ValueKind == JsonValueKind.String ? new Ma2Preview(ma2.GetString()!) : null;
    var inote = fallback?.Generate() ?? data.GetProperty("Inote").GetString()!;
    // Default 是 Sinmai-Alpha 扩展：预览使用本窗口的默认显示选项。
    var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        ["COMBODISPLAY"]="Combo", ["INNERBRIGHTNESS"]="0.4", ["OUTERBRIGHTNESS"]="1",
        ["SHOWJUDGETEXT"]="True", ["SHOWJUDGELINE"]="True", ["SHOWCOMBOINFO"]="False", ["SHOWJUDGEINFO"]="False"
    };
    if(data.TryGetProperty("PreviewDefaults",out var prefs)) {
        if(prefs.TryGetProperty("Combo",out var combo)) defaults["COMBODISPLAY"]=combo.GetBoolean()?"Combo":"None";
        if(prefs.TryGetProperty("BackgroundDim",out var dim)) defaults["INNERBRIGHTNESS"]=(1-dim.GetDouble()).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    inote = Regex.Replace(inote, @"<([A-Z]+)(\*?)\(Default(?=[,)])", m => defaults.TryGetValue(m.Groups[1].Value, out var value) ? "<" + m.Groups[1].Value + m.Groups[2].Value + "(" + value : m.Value, RegexOptions.IgnoreCase);
    process.GetMethod("Serialize")!.Invoke(null, new object[] { inote, 0L });
    var notes = (IEnumerable)process.GetField("notelist", flags)!.GetValue(null)!;
    foreach (var timing in notes)
    {
        timing.GetType().GetMethod("getNotes")!.Invoke(timing, null);
        var error = timing.GetType().GetField("noteParseError")?.GetValue(timing)?.ToString();
        if (!string.IsNullOrEmpty(error)) throw new InvalidDataException(error);
    }
    var jsonType = assembly.GetType("MajdataEdit.Majson", true)!;
    var output = Activator.CreateInstance(jsonType)!;
    foreach (var field in jsonType.GetFields(BindingFlags.Public | BindingFlags.Instance))
    {
        var from = process.GetField(field.Name == "timingList" ? "notelist" : field.Name, flags);
        if (from != null && from.FieldType == field.FieldType) field.SetValue(output, from.GetValue(null));
    }
    jsonType.GetField("filePath")?.SetValue(output, Path.GetFullPath(args[2]));
    if (data.TryGetProperty("Level", out var level)) jsonType.GetField("level")?.SetValue(output, level.GetString());
    var jsonAssembly = Assembly.LoadFrom(Path.Combine(parserDir, "Newtonsoft.Json.dll"));
    var jsonConvert = jsonAssembly.GetType("Newtonsoft.Json.JsonConvert", true)!;
    var text = (string)jsonConvert.GetMethod("SerializeObject", new[] { typeof(object) })!.Invoke(null, new[] { output })!;
    if (fallback != null) text = fallback.ApplyTables(text,defaults["COMBODISPLAY"]=="Combo"?1:0,float.Parse(defaults["INNERBRIGHTNESS"],System.Globalization.CultureInfo.InvariantCulture));
    return text;
    }
    string result;
    try { result = Render(string.IsNullOrWhiteSpace(data.GetProperty("Inote").GetString())); }
    catch (Exception error) when (!string.IsNullOrWhiteSpace(data.GetProperty("Inote").GetString()) && data.TryGetProperty("Ma2",out var backup) && backup.ValueKind == JsonValueKind.String)
    {
        Console.Error.WriteLine("Source syntax could not be parsed; using the current MA2: " + error.GetBaseException().Message);
        result = Render(true);
    }
    File.WriteAllText(args[2], result);
    Console.WriteLine("Preview chart generated");
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.GetBaseException().ToString()); return 1; }
