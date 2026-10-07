using System.Text.RegularExpressions;

namespace MaiChartManager.Services;

public static class AlphaChartAssets
{
    // 只导入谱面显式引用的图片，保留相对目录，不上传编辑器缓存或整套媒体。
    public static string[] References(string source) => Regex.Matches(source, @"~\[([^\[\]\r\n]+\.(?:png|jpe?g|svg))\]", RegexOptions.IgnoreCase)
        .Select(match => Normalize(match.Groups[1].Value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static string Normalize(string name)
    {
        name = name.Trim().Replace('\\', '/');
        var parts = name.Split('/');
        if (parts.Any(p => string.IsNullOrWhiteSpace(p) || p is "." or ".." || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) || Path.IsPathRooted(name))
            throw new ArgumentException(AlphaText.Get("AlphaSkinRelative") + name);
        return name;
    }

    public static List<(string Name, byte[] Data)> ReadUploads(string source, List<IFormFile>? uploads)
    {
        var result = new List<(string, byte[])>();
        var expected = References(source);
        var files = (uploads ?? []).ToDictionary(f => Normalize(f.FileName), StringComparer.OrdinalIgnoreCase);
        foreach (var name in expected)
        {
            if (!files.TryGetValue(name, out var file)) throw new ArgumentException(AlphaText.Get("AlphaSkinMissing") + name);
            if (file.Length is <= 0 or > 32 * 1024 * 1024) throw new ArgumentException(AlphaText.Get("AlphaSkinSize") + name);
            using var stream = file.OpenReadStream();
            using var data = new MemoryStream(); stream.CopyTo(data);
            result.Add((name, data.ToArray()));
        }
        return result;
    }

    public static void Install(string folder, List<(string Name, byte[] Data)> assets)
    {
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var asset in assets)
        {
            var path = Path.GetFullPath(Path.Combine(root, Normalize(asset.Name)));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(AlphaText.Get("AlphaSkinOutside"));
            var directory = Path.GetDirectoryName(path)!;
            for (var current = new DirectoryInfo(directory); current != null && current.FullName.Length >= root.Length - 1; current = current.Parent)
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException(AlphaText.Get("AlphaSkinLink"));
            Directory.CreateDirectory(directory);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, asset.Data); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
