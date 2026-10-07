extern alias Alpha;
using System.Globalization;
using System.Text.RegularExpressions;
using MuConvert.mai;
using Extended = Alpha::MuConvert.mai;

namespace MaiChartManager.Services;

public static class ChartConversion
{
    public static void SetLocale(CultureInfo culture)
    {
        MuConvert.utils.Utils.SetLocale(culture);
        Alpha::MuConvert.utils.Utils.SetLocale(culture);
    }

    // MA2 导出和标准预览按实际记录选择转谱器，普通谱始终使用原版。
    public static bool IsAlphaMa2(string text)
    {
        var native = new HashSet<string>(StringComparer.Ordinal)
        {
            "VERSION", "FES_MODE", "BPM_DEF", "MET_DEF", "RESOLUTION", "CLK_DEF", "COMPATIBLE_CODE", "GENERATED_BY",
            "BPM", "MET", "CLK", "SEF", "T_REC_TAP", "T_REC_BRK", "T_REC_HLD", "T_REC_SLD", "T_REC_ALL", "T_NUM_TAP",
            "T_NUM_BRK", "T_NUM_HLD", "T_NUM_SLD", "T_NUM_ALL", "T_JUDGE_TAP", "T_JUDGE_HLD", "T_JUDGE_SLD", "T_JUDGE_ALL",
            "TTM_EACHPAIRS", "TTM_SCR_TAP", "TTM_SCR_BRK", "TTM_SCR_HLD", "TTM_SCR_SLD", "TTM_SCR_ALL", "TTM_SCR_S",
            "TTM_SCR_SS", "TTM_RAT_ACV", "TTM_SCR_BONUS", "TTM_SCR_BNS", "TTM_SCR_DX", "TTM_SCR_DXS",
        };
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
            var fields = Regex.Split(line, @"\s+"); var tag = fields[0];
            if (native.Contains(tag) || tag.StartsWith("T_REC_") || tag.StartsWith("T_NUM_") || tag.StartsWith("T_JUDGE_") || tag.StartsWith("TTM_")) continue;
            if (Regex.IsMatch(tag, @"^(?:(?:NM|BR|EX|BX)(?:TAP|HLD|STR)|NM(?:THO|TTP)|(?:NM|BR|CN)(?:SI_|SCL|SCR|SUL|SUR|SSL|SSR|SV_|SXL|SXR|SLL|SLR|SF_))$"))
            {
                // 原生 note 的字段后附加 DZ/FK/流速/独立流等也属于 Alpha。
                var expected = tag.EndsWith("TAP") || tag.EndsWith("STR") ? 4 : tag.EndsWith("HLD") ? 5 : tag.EndsWith("TTP") ? 7 : tag.EndsWith("THO") ? 8 : 7;
                if (fields.Length > expected) return true;
                continue;
            }
            // 旧版 MA2 1.03 的标准记录也交给原版。
            if (Regex.IsMatch(tag, @"^(?:TAP|BRK|HLD|STR|BST|XTP|XST|XHO|TTP|THO|SI_|SCL|SCR|SUL|SUR|SSL|SSR|SV_|SXL|SXR|SLL|SLR|SF_)$"))
            {
                var expected = tag is "TAP" or "BRK" or "STR" or "BST" or "XTP" or "XST" ? 4
                    : tag is "HLD" or "XHO" ? 5 : tag == "THO" ? 8 : 7;
                if (fields.Length > expected) return true;
                continue;
            }
            return true;
        }
        return false;
    }

    public static string ToSimai(string ma2) => ToSimai(ma2, out _);

    public static string ToSimai(string ma2, out int clockCount)
    {
        if (!IsAlphaMa2(ma2))
        {
            var (chart, _) = new MA2Parser().Parse(ma2);
            clockCount = chart.ClockCount;
            return new SimaiGenerator().Generate(chart).Item1;
        }
        var (extended, _) = new Extended.MA2Parser().Parse(ma2);
        clockCount = extended.ClockCount;
        return new Extended.SimaiGenerator().Generate(extended).Item1;
    }
    public static string ToLegacyMa2(string ma2)
    {
        if (!IsAlphaMa2(ma2)) return new MA2_103Generator().Generate(new MA2Parser().Parse(ma2).Item1).Item1;
        return new Extended.MA2_103Generator().Generate(new Extended.MA2Parser().Parse(ma2).Item1).Item1;
    }
}
