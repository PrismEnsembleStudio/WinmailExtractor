using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinmailExtractor.Core.Services;

public enum RegistrationState { NotRegistered, Registered, NeedsRegistration, ForeignKey }
public sealed record RegistrationInfo(RegistrationState State, string? ExecutablePath);

public sealed class ContextMenuRegistrationService
{
    public const string KeyPath = @"Software\Classes\SystemFileAssociations\.dat\shell\WinmailExtractor";
    private const string Owner = "WinmailExtractor.Portable.v1";
    private readonly RegistryKey root;
    private readonly string keyPath;
    private readonly Action notify;

    public ContextMenuRegistrationService() : this(Registry.CurrentUser, KeyPath, Notify) { }
    // Tests use a temporary subtree with no shell association or notification.
    internal ContextMenuRegistrationService(RegistryKey root, string keyPath, Action notify)
    { this.root = root; this.keyPath = keyPath; this.notify = notify; }

    public RegistrationInfo GetStatus(string executablePath)
    {
        using var key = root.OpenSubKey(keyPath);
        if (key == null) return new(RegistrationState.NotRegistered, null);
        if (key.GetValue("Owner") as string != Owner) return new(RegistrationState.ForeignKey, null);
        using var command = key.OpenSubKey("command");
        string? registered = key.GetValue("ExecutablePath") as string;
        return Evaluate(executablePath, registered, command?.GetValue("") as string,
            key.GetValue("") as string == "Winmail Extractorで開く" && key.GetValue("Icon") as string == $"\"{executablePath}\",0");
    }

    public static RegistrationInfo Evaluate(string current, string? registered, string? command, bool metadataValid = true) =>
        new(metadataValid && string.Equals(current, registered, StringComparison.OrdinalIgnoreCase)
            && string.Equals(BuildCommand(current), command, StringComparison.OrdinalIgnoreCase)
            && File.Exists(current) ? RegistrationState.Registered : RegistrationState.NeedsRegistration, registered);

    public static string BuildCommand(string executablePath)
    {
        if (!Path.IsPathFullyQualified(executablePath) || executablePath.Contains('"') || executablePath.Contains('%')
            || executablePath.IndexOfAny(['\r', '\n', '\0']) >= 0 || !executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("実行ファイルの絶対パスが必要です。パスに % や引用符は使用できません。");
        return $"\"{executablePath}\" \"%1\"";
    }

    public void Register(string executablePath)
    {
        string commandText = BuildCommand(executablePath);
        if (!File.Exists(executablePath)) throw new IOException("登録先のEXEが見つかりません。");
        if (GetStatus(executablePath).State == RegistrationState.ForeignKey)
            throw new InvalidOperationException("同じ名前の別の登録が存在するため、変更しませんでした。");
        using var key = root.CreateSubKey(keyPath, true);
        key.SetValue("Owner", Owner, RegistryValueKind.String);
        key.SetValue("", "Winmail Extractorで開く", RegistryValueKind.String);
        key.SetValue("Icon", $"\"{executablePath}\",0", RegistryValueKind.String);
        key.SetValue("ExecutablePath", executablePath, RegistryValueKind.String);
        key.SetValue("MultiSelectModel", "Single", RegistryValueKind.String);
        using var command = key.CreateSubKey("command", true);
        command.SetValue("", commandText, RegistryValueKind.String);
        notify();
    }

    public void Unregister(string executablePath)
    {
        var state = GetStatus(executablePath).State;
        if (state == RegistrationState.NotRegistered) return;
        if (state == RegistrationState.ForeignKey) throw new InvalidOperationException("このアプリが登録したキーではないため、解除しませんでした。");
        root.DeleteSubKeyTree(keyPath, false);
        notify();
    }

    private static void Notify() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
