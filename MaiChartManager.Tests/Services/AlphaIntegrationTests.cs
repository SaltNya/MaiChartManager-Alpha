using MaiChartManager.Controllers.Charts.Services;
using MaiChartManager.Models;
using MaiChartManager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace MaiChartManager.Tests.Services;

[Collection("Alpha global settings")]
public sealed class AlphaIntegrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mcm-alpha-test-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void PerChartMarkersOverrideOnlyTheirOwnDifficulty()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "ExtraDifficulty.flag"), "Inscribed");
        var master = Path.Combine(root, "song_03.ma2");
        var expert = Path.Combine(root, "song_02.ma2");
        Assert.Equal("Inscribed", SinmaiAlphaIntegration.GetTheme(master, 3));
        Assert.Equal("None", SinmaiAlphaIntegration.GetTheme(expert, 2));
        var previousGamePath = StaticSettings.GamePath;
        try
        {
            StaticSettings.GamePath = root;
            var themePath = Path.Combine(root, "Sinmai-Alpha", "ExtraDifficulty", "ArbitraryTheme");
            Directory.CreateDirectory(themePath);
            File.WriteAllText(Path.Combine(themePath, "difficulty.json"), "{\"name\":\"Custom\",\"textureCode\":\"CUS\"}");
            SinmaiAlphaIntegration.SetTheme(expert, "ArbitraryTheme", 2);
        }
        finally { StaticSettings.GamePath = previousGamePath; }
        SinmaiAlphaIntegration.SetTheme(master, "None");
        Assert.Equal("ArbitraryTheme", SinmaiAlphaIntegration.GetTheme(expert, 2));
        Assert.Equal("None", SinmaiAlphaIntegration.GetTheme(master, 3));
        Assert.Throws<ArgumentException>(() => SinmaiAlphaIntegration.SetTheme(master, "../Strong"));
    }
    [Fact]
    public void ImportPreservesPreviewMetadataAndRejectsStaleChart()
    {
        var music = MusicXml.CreateNew(16099, root, "A601");
        const string inote = "(120){4}<ShowJudgeInfo*(True,0)><OuterBrightness*(0.5,1)><ComboDisplay*(DxScore,8:1)>1,2,E";
        var text = "&title=Alpha test\n&artist=Test artist\n&first=0.06\n&lv_5=14\n&inote_5=" + inote;
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        using var stream = new MemoryStream(bytes);
        var result = new MaidataImportService(NullLogger<MaidataImportService>.Instance).ImportMaidata(music, new FormFile(stream, 0, bytes.Length, "file", "maidata.txt"), ShiftMethod.NoShift, false, false, useAlpha: true);
        Assert.False(result.Fatal);
        var path = SinmaiAlphaIntegration.ChartPath(music, 3);
        var source = SinmaiAlphaIntegration.ReadSource(path);
        Assert.NotNull(source);
        Assert.Equal("Alpha test", source.Title);
        Assert.Equal("Test artist", source.Artist);
        Assert.Equal(inote, source.Inote);
        var chart = File.ReadAllText(path);
        Assert.Contains("COMBODISPLAY", chart);
        Assert.DoesNotContain("SHOWJUDGEINFO", chart);
        Assert.DoesNotContain("OUTERBRIGHTNESS", chart);
        File.AppendAllText(path, "\n");
        Assert.Null(SinmaiAlphaIntegration.ReadSource(path));
        music.Charts[3].Path = "../escape.ma2";
        Assert.Throws<ArgumentException>(() => SinmaiAlphaIntegration.ChartPath(music, 3));
    }
    public void Dispose()
    {
        var full = Path.GetFullPath(root);
        if (Path.GetDirectoryName(full) == Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) && Path.GetFileName(full).StartsWith("mcm-alpha-test-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
