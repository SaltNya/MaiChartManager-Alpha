extern alias Alpha;
using MuConvert.mai;
using MuConvert.utils;
using Rationals;
using Extended = Alpha::MuConvert.mai;
using ExtendedUtils = Alpha::MuConvert.utils;

namespace MaiChartManager.Controllers.Charts.Services;

// 原版与 Alpha 分别解析、偏移、生成，不互转模型，也不静默切换转谱器。
public sealed class ImportParsedChart
{
    private readonly Extended.MaiChart? alpha;
    private readonly MaiChart? original;
    private ImportParsedChart(Extended.MaiChart chart) { alpha = chart; }
    private ImportParsedChart(MaiChart chart) { original = chart; }
    public decimal StartBpm => alpha?.StartBpm ?? original!.StartBpm;
    public Rational StartBar => alpha != null ? alpha.StartTime.InvariantBar : original!.StartTime.InvariantBar;
    public int TotalNotes => alpha?.TotalNotes ?? original!.TotalNotes;
    public int StatisticsTotal => alpha?.Statistics.Total ?? original!.Statistics.Total;
    public bool IsDxChart => alpha?.IsDxChart ?? original!.IsDxChart;
    public bool UsesAlpha => alpha != null;
    public static (ImportParsedChart, List<Alert>) Parse(string inote, bool bigTouch, int clockCount, bool useAlpha)
    {
        if (!useAlpha) { var (chart, alerts) = new SimaiParser(bigTouch, clockCount).Parse(inote); return (new(chart), alerts); }
        try { var (chart, alerts) = new Extended.SimaiParser(bigTouch, clockCount).Parse(inote); return (new(chart), ConvertAlerts(alerts)); }
        catch (ExtendedUtils.ConversionException e) { throw new ConversionException(ConvertAlerts(e.Alerts), e); }
    }
    public void Shift(Rational bar, decimal bpm)
    {
        if (original != null) { original.Shift(bar, bpm); return; }
        try { alpha!.Shift(bar, bpm); }
        catch (ExtendedUtils.ConversionException e) { throw new ConversionException(ConvertAlerts(e.Alerts), e); }
    }
    public (string, List<Alert>) Generate(bool isUtage = false)
    {
        if (original != null) return new MA2Generator(isUtage).Generate(original);
        try { var (ma2, alerts) = new Extended.MA2Generator(isUtage).Generate(alpha!); return (ma2, ConvertAlerts(alerts)); }
        catch (ExtendedUtils.ConversionException e) { throw new ConversionException(ConvertAlerts(e.Alerts), e); }
    }
    private static List<Alert> ConvertAlerts(IEnumerable<ExtendedUtils.Alert> alerts) => alerts.Select(a => new Alert(
        (Alert.LEVEL)(int)a.Level, a.Description, a.TimeInBar, a.TimeInSeconds, a.Line, a.RelevantNote)).ToList();
}
