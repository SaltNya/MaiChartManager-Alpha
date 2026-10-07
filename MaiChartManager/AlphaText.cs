using System.Globalization;

namespace MaiChartManager;

internal static class AlphaText
{
    public static string Get(string key) => Locale.ResourceManager.GetString(key, Locale.Culture ?? CultureInfo.CurrentUICulture) ?? key;
}
