using System.Globalization;
using System.Text;

namespace WinmailExtractor.Core;

public static class FileNamePolicy
{
    private static readonly HashSet<string> Dangerous = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
        ".msi", ".msp", ".scr", ".pif", ".cpl", ".dll", ".hta", ".lnk", ".url", ".reg", ".scf", ".chm",
        ".jar", ".application", ".appref-ms", ".docm", ".xlsm", ".pptm", ".xlam", ".xll", ".iso", ".img"
    };

    public static bool RequiresCaution(string name) => Dangerous.Contains(Path.GetExtension(name));

    public static string Sanitize(string? input, int index = 1)
    {
        // Flatten both Windows and Unix paths; never interpret attachment names as paths.
        string name = input ?? "";
        int separator = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        name = name[(separator + 1)..];
        var text = new StringBuilder();
        foreach (char c in name)
        {
            var category = char.GetUnicodeCategory(c);
            if (char.IsControl(c) || category == UnicodeCategory.Format || "<>:\"/\\|?*".Contains(c))
                text.Append('_');
            else
                text.Append(c);
        }
        name = text.ToString().Trim().TrimEnd('.', ' ');
        if (name.Length == 0 || name is "." or "..") name = $"attachment-{index:D3}.bin";
        string stem = name.Split('.')[0].TrimEnd(' ', '.');
        if (IsReserved(stem)) name = "_" + name;
        return Shorten(name, 160);
    }

    private static bool IsReserved(string stem)
    {
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase)) return true;
        if (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
            return "123456789¹²³".Contains(stem[3]);
        return false;
    }

    public static string Shorten(string name, int maxLength)
    {
        if (name.Length <= maxLength) return name;
        string extension = Path.GetExtension(name);
        if (extension.Length > 20) extension = "";
        int count = maxLength - extension.Length;
        if (char.IsHighSurrogate(name[count - 1])) count--;
        return name[..count].TrimEnd('.', ' ') + extension;
    }
}
