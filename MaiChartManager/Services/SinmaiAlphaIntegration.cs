using System.Reflection;
using SinmaiAlpha.Assets;
using System.Security.Cryptography;
using System.Text.Json;
using MaiChartManager.Models;

namespace MaiChartManager.Services;

public record SinmaiAlphaStatus(bool Installed, string? Version, bool AssetsReady, bool SuppressNotice, string PackagePath, bool PreviewReady);
public record AlphaPreviewSource(int Schema, string ChartSha256, string Inote, double OffsetSeconds, string Title, string Artist, string Designer, string Level, string ClockCount, string? Ma2 = null);

public static class SinmaiAlphaIntegration
{
    public static string PreviewRoot => Path.Combine(StaticSettings.exeDir, "AlphaPreview");
    public static SinmaiAlphaStatus Status()
    {
        var package = StaticSettings.GamePath;
        var installed = false;
        string? version = null;
        var path = Path.Combine(package, "Mods", "Sinmai-Alpha.dll");
        if (!string.IsNullOrWhiteSpace(package) && File.Exists(path))
        {
            try { var name = AssemblyName.GetAssemblyName(path); installed = name.Name == "Sinmai-Alpha"; version = name.Version?.ToString(); }
            catch (Exception e) when (e is IOException or BadImageFormatException or UnauthorizedAccessException) { }
        }
        var assets = new[] { "ExtraDifficulty", "CustomNoteTypes", "Alpha" }.All(d => Directory.Exists(Path.Combine(package, "Sinmai-Alpha", d)));
        var preview = File.Exists(Path.Combine(PreviewRoot,"UI","controls.json")) && File.Exists(Path.Combine(PreviewRoot,"Viewer","MajdataView_Data","Managed","SinmaiAlpha.PreviewPlayer.dll")) && OperatingSystem.IsWindows() && File.Exists(Path.Combine(PreviewRoot, "Viewer", "MajdataView.exe")) && File.Exists(Path.Combine(PreviewRoot, "Bridge", "SinmaiAlpha.PreviewBridge.exe"));
        return new(installed, version, assets, StaticSettings.Config.SuppressSinmaiAlphaNotice, package, preview);
    }

    public static string ChartPath(MusicXml music, int level, string? side = null)
    {
        if (level < 0 || level >= music.Charts.Length) throw new ArgumentOutOfRangeException(nameof(level));
        if (side is not (null or "L" or "R")) throw new ArgumentException(AlphaText.Get("AlphaSideInvalid"));
        var directory = Path.GetFullPath(Path.GetDirectoryName(music.FilePath)!);
        var name = music.Charts[level].Path;
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(AlphaText.Get("AlphaChartPathMissing"));
        if (side != null) name = Path.ChangeExtension(name, null) + "_" + side + ".ma2";
        var path = Path.GetFullPath(Path.Combine(directory, name));
        if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(AlphaText.Get("AlphaChartPathOutside"));
        return path;
    }
    public static string MarkerPath(string chart) => Path.ChangeExtension(chart, ".ExtraDifficulty.flag");
    public static string GetTheme(string chart, int level)
    {
        var marker = MarkerPath(chart);
        if (!File.Exists(marker) && level == 3) marker = Path.Combine(Path.GetDirectoryName(chart)!, "ExtraDifficulty.flag");
        if (!File.Exists(marker)) return "None";
        var text = ChartFlags.Parse(File.ReadAllText(marker)).Theme;
        return string.Equals(text,"None",StringComparison.OrdinalIgnoreCase)?"None":DifficultyThemeCatalog.Resolve(Themes(),text)?.Id ?? (text.Length==0?"Strong":text);
    }
    public static List<DifficultyTheme> Themes()=>DifficultyThemeCatalog.Read(Path.Combine(StaticSettings.GamePath,"Sinmai-Alpha","ExtraDifficulty"),text=>JsonSerializer.Deserialize<DifficultyTheme>(text,new JsonSerializerOptions{IncludeFields=true})!);
    private static readonly object MarkerLock = new();
    private static ChartFlags ReadFlags(string chart, int level)
    {
        var marker = MarkerPath(chart);
        if (!File.Exists(marker) && level == 3) marker = Path.Combine(Path.GetDirectoryName(chart)!, "ExtraDifficulty.flag");
        return File.Exists(marker) ? ChartFlags.Parse(File.ReadAllText(marker)) : new ChartFlags();
    }
    public static bool? GetTapInHold(string chart, int level) => ReadFlags(chart, level).TapInHoldOverride;
    public static void SetTapInHold(string chart, int level, bool? allowed)
    {
        if (chart.Replace('\\', '/').Split('/').Any(part => part.Equals("A000", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException(AlphaText.Get("AlphaA000Disabled"));
        if (!File.Exists(chart)) throw new ArgumentException(AlphaText.Get("AlphaDifficultyChartMissing"));
        lock (MarkerLock) AtomicWrite(MarkerPath(chart), ReadFlags(chart, level).WithTapInHold(allowed));
    }
    public static void SetTheme(string chart, string theme, int level = 3)
    {
        var resolved = string.Equals(theme,"None",StringComparison.OrdinalIgnoreCase)?"None":DifficultyThemeCatalog.Resolve(Themes(),theme)?.Id;
        if(resolved==null)throw new ArgumentException(AlphaText.Get("AlphaThemeDirectoryMissing"));
        // None 是显式覆盖，不能删除后意外回退到旧版整曲 MASTER 标记。
        lock (MarkerLock) AtomicWrite(MarkerPath(chart), ReadFlags(chart, level).WithTheme(resolved));
    }
    public static void AtomicWrite(string path, string content)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, content); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    public static void SaveSource(string chartPath, string inote, double offset, MusicXml music, string designer, string level, string clockCount, string? title = null, string? artist = null)
    {
        var source = new AlphaPreviewSource(1, Hash(chartPath), inote, offset, title ?? music.Name, artist ?? music.Artist, designer, level, clockCount);
        WriteSourceCache(source);
        var legacy = Path.ChangeExtension(chartPath, ".alpha-preview.json");
        if (File.Exists(legacy)) File.Delete(legacy);
    }
    public static string SourceCacheRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MaiChartManager", "AlphaPreviewSources");
    private static string SourceCachePath(string hash) => Path.Combine(SourceCacheRoot, hash + ".json");
    private static void WriteSourceCache(AlphaPreviewSource source)
    {
        Directory.CreateDirectory(SourceCacheRoot);
        AtomicWrite(SourceCachePath(source.ChartSha256), JsonSerializer.Serialize(source));
    }
    public static AlphaPreviewSource? ReadSource(string chartPath)
    {
        var hash = Hash(chartPath);
        var legacy = Path.ChangeExtension(chartPath, ".alpha-preview.json");
        foreach (var path in new[] { legacy, SourceCachePath(hash) })
        {
            if (!File.Exists(path)) continue;
            AlphaPreviewSource? source;
            try { source = JsonSerializer.Deserialize<AlphaPreviewSource>(File.ReadAllText(path)); }
            catch (JsonException) { continue; }
            if (source is not { Schema: 1 } || source.ChartSha256 != hash) continue;
            if (path == legacy)
            {
                // 原始语法先持久保存，成功后才移走歌曲目录中的旧预览文件。
                WriteSourceCache(source);
                File.Delete(legacy);
            }
            return source;
        }
        return null;
    }
}
