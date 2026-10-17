using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WinmailExtractor.ViewModels;
using WinmailExtractor.Views;

namespace WinmailExtractor;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();
    public MainWindow(string[] args)
    {
        InitializeComponent();
        DataContext = ViewModel;
        Closing += (_, e) =>
        {
            if (ViewModel.Busy) { e.Cancel = true; ViewModel.Report("処理中です。完了してからウィンドウを閉じてください。"); }
        };
        Loaded += async (_, _) =>
        {
            if (args.Length == 1) await ViewModel.LoadAsync(args[0]);
            else if (args.Length > 1) ViewModel.Report("一度に開けるのは1ファイルです。ファイルを1つ選択してください。", true);
        };
    }
    private async void ChooseFile(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanInteract) return;
        var dialog = new OpenFileDialog { Title = "winmail.dat を選択", Filter = "TNEFファイル (*.dat;*.tnef)|*.dat;*.tnef|すべてのファイル (*.*)|*.*", Multiselect = false, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) await ViewModel.LoadAsync(dialog.FileName);
    }
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = ViewModel.CanInteract && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!ViewModel.CanInteract) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files) await ViewModel.LoadAsync(files[0]);
        else ViewModel.Report("ファイルを1つだけドロップしてください。", true);
    }
    private async void SaveFiles(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanSave) return;
        var dialog = new OpenFolderDialog { Title = "添付ファイルの保存先", Multiselect = false };
        if (dialog.ShowDialog(this) == true) await ViewModel.SaveAsync(dialog.FolderName);
    }
    private void OpenSavedDirectory(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SavedDirectory == null) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { ViewModel.SavedDirectory }, UseShellExecute = false }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { ViewModel.Report("保存先を開けませんでした。エクスプローラーから確認してください。", true); }
    }
    private void OpenSettings(object sender, RoutedEventArgs e) => new SettingsWindow { Owner = this }.ShowDialog();
    private void SelectAll(object sender, RoutedEventArgs e) => ViewModel.SelectAll(true);
    private void SelectNone(object sender, RoutedEventArgs e) => ViewModel.SelectAll(false);
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { ChooseFile(sender, e); e.Handled = true; }
    }
}
