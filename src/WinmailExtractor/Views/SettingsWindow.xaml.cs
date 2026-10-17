using System.IO;
using System.Windows;
using WinmailExtractor.Core.Services;

namespace WinmailExtractor.Views;

public partial class SettingsWindow : Window
{
    private readonly ContextMenuRegistrationService service = new();
    private readonly string executable = Environment.ProcessPath ?? "";
    public SettingsWindow()
    {
        InitializeComponent();
        CurrentPath.Text = executable;
        using (var stream = typeof(SettingsWindow).Assembly.GetManifestResourceStream("WinmailExtractor.License"))
        {
            if (stream != null) { using var reader = new StreamReader(stream); LicenseText.Text = reader.ReadToEnd() + "\n\nThird-party software\n\n"; }
        }
        using (var stream = typeof(SettingsWindow).Assembly.GetManifestResourceStream("WinmailExtractor.ThirdPartyNotices"))
        {
            if (stream != null) { using var reader = new StreamReader(stream); LicenseText.Text += reader.ReadToEnd(); }
        }
        Refresh();
    }
    private void Refresh()
    {
        try
        {
            var status = service.GetStatus(executable);
            StateText.Text = status.State switch
            {
                RegistrationState.Registered => "登録済みです",
                RegistrationState.NeedsRegistration => "再登録が必要です（EXEの移動・登録内容の変更）",
                RegistrationState.ForeignKey => "同名の別の登録が存在します。安全のため変更できません。",
                _ => "未登録です"
            };
            RegisteredPath.Text = status.ExecutablePath == null ? "" : $"登録先: {status.ExecutablePath}";
            RegisterButton.IsEnabled = status.State != RegistrationState.ForeignKey;
            UnregisterButton.IsEnabled = status.State is RegistrationState.Registered or RegistrationState.NeedsRegistration;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            StateText.Text = "登録状態を確認できません。レジストリへのアクセス権を確認してください。";
            RegisterButton.IsEnabled = UnregisterButton.IsEnabled = false;
        }
    }
    private void Register(object sender, RoutedEventArgs e) => Execute(() => service.Register(executable), "登録しました。Windows 11では「その他のオプションを確認」から選択できます。");
    private void Unregister(object sender, RoutedEventArgs e) => Execute(() => service.Unregister(executable), "このアプリの右クリック連携を解除しました。");
    private void Execute(Action action, string success)
    {
        try { action(); Notice.Text = success; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException or ArgumentException)
        { Notice.Text = $"操作できませんでした。{ex.Message}"; }
        Refresh();
    }
}
