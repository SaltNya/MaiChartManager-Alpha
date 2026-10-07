extern alias Alpha;
using System.Globalization;
using MaiChartManager.Controllers.Charts.Services;
using MaiChartManager.Services;
using MuConvert.mai;
using MuConvert.utils;

namespace MaiChartManager.Tests.Services;

[CollectionDefinition("Alpha global settings", DisableParallelization = true)]
public sealed class AlphaGlobalSettingsCollection;

[Collection("Alpha global settings")]
public sealed class ChartConversionTests
{
    [Fact]
    public void ConverterAssembliesAreIndependent()
    {
        Assert.Equal("MuConvert", typeof(MaiChart).Assembly.GetName().Name);
        Assert.Equal("MuConvert.Alpha", typeof(Alpha::MuConvert.mai.MaiChart).Assembly.GetName().Name);
        Assert.NotEqual(typeof(MaiChart), typeof(Alpha::MuConvert.mai.MaiChart));
    }

    [Fact]
    public void OrdinaryImportMatchesUnmodifiedConverter()
    {
        const string inote = "(120){4}1,2b,3h[4:1],4-8[4:1],C,E";
        var (chart, _) = ImportParsedChart.Parse(inote, false, 4, false);
        var expected = new MA2Generator().Generate(new SimaiParser(false, 4).Parse(inote).Item1).Item1;
        Assert.False(chart.UsesAlpha);
        Assert.Equal(expected, chart.Generate().Item1);
        Assert.False(ChartConversion.IsAlphaMa2(expected));
        Assert.Equal(new SimaiGenerator().Generate(new MA2Parser().Parse(expected).Item1).Item1, ChartConversion.ToSimai(expected, out var count));
        Assert.Equal(4, count);
        Assert.Equal(new MA2_103Generator().Generate(new MA2Parser().Parse(expected).Item1).Item1, ChartConversion.ToLegacyMa2(expected));
    }

    [Theory]
    [InlineData("(120){4}1m,2,E", "MNTAP")]
    [InlineData("(120){4}B4$,1,E", "NMSTP")]
    [InlineData("(120){4}2-B6[4:1],1,E", "NMSSS")]
    [InlineData("(120){4}<ComboDisplay*(Combo,0)>1,2,E", "COMBODISPLAY")]
    public void ExplicitAlphaImportRetainsExtendedRecords(string inote, string record)
    {
        Assert.True(AlphaContentDetector.ContainsAlpha(inote));
        var (chart, _) = ImportParsedChart.Parse(inote, false, 6, true);
        Assert.True(chart.UsesAlpha);
        var ma2 = chart.Generate().Item1;
        Assert.Contains(record + "\t", ma2);
        Assert.True(ChartConversion.IsAlphaMa2(ma2));
        // SSS 的 MA2 恢复由 Alpha 预览桥接器处理，不属于通用 MA2→simai 导出。
        if (record != "NMSSS")
        {
            var expected = new Alpha::MuConvert.mai.SimaiGenerator().Generate(new Alpha::MuConvert.mai.MA2Parser().Parse(ma2).Item1).Item1;
            Assert.Equal(expected, ChartConversion.ToSimai(ma2, out var count));
            Assert.Equal(6, count);
        }
    }

    [Fact]
    public void DecliningAlphaDoesNotSilentlySwitchConverter()
    {
        // 原版的宽松解析可能警告并忽略不认识的修饰符，应完整保留原版行为。
        const string inote = "(120){4}1m,2,E";
        var (chart, alerts) = ImportParsedChart.Parse(inote, false, 4, false);
        var (native, nativeAlerts) = new SimaiParser(false, 4).Parse(inote);
        Assert.False(chart.UsesAlpha);
        Assert.Equal(nativeAlerts.Select(a => a.Description), alerts.Select(a => a.Description));
        Assert.Equal(new MA2Generator().Generate(native).Item1, chart.Generate().Item1);
        Assert.DoesNotContain("MNTAP", chart.Generate().Item1);
    }

    [Theory]
    [InlineData("(120){4}1,2,|| <SV*(2)> B4$\n3,E")]
    [InlineData("(120){4}1,|* <ComboDisplay*(None,0)> *|2,E")]
    public void CommentExamplesDoNotTriggerAlpha(string inote) => Assert.False(AlphaContentDetector.ContainsAlpha(inote));

    [Fact]
    public void OriginalMa2FixturesKeepOriginalConverter()
    {
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NativeMa2"), "*.ma2", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var file in files) Assert.False(ChartConversion.IsAlphaMa2(File.ReadAllText(file)), file);
    }

    [Theory]
    [InlineData("NMTAP\t1\t0\t0\tDZ")]
    [InlineData("TAP\t1\t0\t0\tFK")]
    [InlineData("SVSP\t1\t0\t2")]
    [InlineData("BRTTP\t1\t0\t0\tB\t0\tM1")]
    public void ExtendedRecordsSelectAlpha(string line) => Assert.True(ChartConversion.IsAlphaMa2(line));

    [Theory]
    [InlineData("en-US", "The chart file does not exist.")]
    [InlineData("zh-CN", "谱面文件不存在")]
    [InlineData("zh-TW", "譜面檔案不存在")]
    public void BackendAndBothConvertersHaveLocalizedResources(string culture, string expected)
    {
        var previous = Locale.Culture;
        try
        {
            var value = new CultureInfo(culture);
            Locale.Culture = value;
            ChartConversion.SetLocale(value);
            Assert.Equal(expected, Locale.ResourceManager.GetString("AlphaChartMissing", value));
            Assert.Equal(value, MuConvert.Locale.Culture);
            Assert.Equal(value, Alpha::MuConvert.Locale.Culture);
        }
        finally { Locale.Culture = previous; ChartConversion.SetLocale(previous ?? CultureInfo.InvariantCulture); }
    }
}
