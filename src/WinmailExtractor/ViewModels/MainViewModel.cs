using System.Collections.ObjectModel;
using System.IO;
using WinmailExtractor.Core.Models;
using WinmailExtractor.Core.Services;

namespace WinmailExtractor.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly TnefExtractionService extractor = new();
    private readonly AttachmentSaveService saver = new();
    public ObservableCollection<AttachmentViewModel> Attachments { get; } = [];
    public bool Busy { get; private set; }
    public bool CanInteract => !Busy;
    public bool HasFile { get; private set; }
    public bool IsEmpty => !HasFile;
    public string SourceName { get; private set; } = "";
    public string Status { get; private set; } = "ファイルを選ぶと、保存する前に中身を確認できます。";
    public bool IsError { get; private set; }
    public string? SavedDirectory { get; private set; }
    public bool HasSavedDirectory => SavedDirectory != null;
    public bool CanSave => !Busy && SelectedCount > 0;
    public int SelectedCount { get { int count = 0; foreach (var item in Attachments) if (item.Selected) count++; return count; } }
    public string SelectionSummary => $"{Attachments.Count}件の添付  /  {SelectedCount}件を選択";

    public async Task LoadAsync(string path)
    {
        if (Busy) return;
        SetBusy(true);
        foreach (var item in Attachments) item.PropertyChanged -= AttachmentChanged;
        Attachments.Clear();
        HasFile = false;
        SavedDirectory = null;
        NotifyAll();
        Report("添付ファイルを解析しています…");
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await Task.Run(() => extractor.Extract(path, timeout.Token));
            foreach (var attachment in result.Attachments)
            {
                var item = new AttachmentViewModel(attachment);
                item.PropertyChanged += AttachmentChanged;
                Attachments.Add(item);
            }
            SourceName = Path.GetFileName(path);
            HasFile = true;
            string message = Attachments.Count == 0 ? "保存できる通常の添付ファイルはありませんでした。" : $"{Attachments.Count}件の添付が見つかりました。保存するファイルを選択してください。";
            if (result.Warnings.Count > 0) message += $"\n{result.Warnings.Count}件の注意: {result.Warnings[0]}";
            Report(message);
        }
        catch (ExtractionException ex) { Report(ex.Message, true); }
        catch (OperationCanceledException) { Report("解析を中止しました。ファイルのサイズと形式を確認してください。", true); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Report("ファイルを読み込めませんでした。ファイル名とアクセス権を確認してください。", true); }
        finally { SetBusy(false); }
    }

    public async Task SaveAsync(string directory)
    {
        if (!CanSave) return;
        var selected = new List<ExtractedAttachment>();
        foreach (var item in Attachments) if (item.Selected) selected.Add(item.Attachment);
        SetBusy(true);
        SavedDirectory = null;
        Report("選択した添付ファイルを保存しています…");
        try
        {
            var result = await Task.Run(() => saver.Save(directory, selected));
            if (result.SavedPaths.Count > 0) SavedDirectory = directory;
            Report(result.Error == null ? $"{result.SavedPaths.Count}件を保存しました。" : $"{result.SavedPaths.Count}件を保存済みです。{result.Error}", result.Error != null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Report("保存処理を完了できませんでした。保存先を確認してください。", true); }
        finally { SetBusy(false); }
    }

    public void SelectAll(bool selected) { foreach (var item in Attachments) item.Selected = selected; }
    public void Report(string message, bool error = false) { Status = message; IsError = error; Changed(nameof(Status)); Changed(nameof(IsError)); }
    private void AttachmentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { Changed(nameof(CanSave)); Changed(nameof(SelectionSummary)); }
    private void SetBusy(bool value) { Busy = value; NotifyAll(); }
    private void NotifyAll()
    {
        foreach (var name in new[] { nameof(Busy), nameof(CanInteract), nameof(CanSave), nameof(HasFile), nameof(IsEmpty), nameof(SourceName), nameof(SelectionSummary), nameof(HasSavedDirectory) }) Changed(name);
    }
}
