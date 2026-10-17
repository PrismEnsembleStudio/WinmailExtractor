using WinmailExtractor.Core.Models;

namespace WinmailExtractor.Core.Services;

public sealed record SaveResult(IReadOnlyList<string> SavedPaths, string? Error);

public sealed class AttachmentSaveService
{
    public SaveResult Save(string directory, IReadOnlyList<ExtractedAttachment> attachments, CancellationToken cancellationToken = default)
    {
        var saved = new List<string>();
        try
        {
            string root = Path.GetFullPath(directory);
            if (!Directory.Exists(root)) throw new IOException("保存先フォルダーが存在しません。");
            // Lock each ancestor against rename/delete while saving and reject junctions/symlinks.
            using var guard = DirectoryGuard.Acquire(root);
            foreach (var attachment in attachments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = FileNamePolicy.Sanitize(attachment.FileName);
                string extension = Path.GetExtension(name);
                string stem = Path.GetFileNameWithoutExtension(name);
                bool created = false;
                for (int n = 1; n <= 10000; n++)
                {
                    string candidate = n == 1 ? name : $"{stem} ({n}){extension}";
                    string path = Path.GetFullPath(Path.Combine(root, candidate));
                    if (!string.Equals(Path.GetDirectoryName(path), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(Path.GetDirectoryName(path), root, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("保存先の外へ書き込むことはできません。");
                    FileStream output;
                    try { output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
                    catch (IOException) when (File.Exists(path) || Directory.Exists(path)) { continue; }
                    try
                    {
                        using (output)
                        {
                            output.Write(attachment.Content);
                            output.Flush(true);
                        }
                    }
                    catch
                    {
                        output.Dispose();
                        // Only a file we just created; ancestors are locked against redirection.
                        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                        throw;
                    }
                    saved.Add(path);
                    created = true;
                    break;
                }
                if (!created) throw new IOException("同名ファイルが多すぎるため、別の保存先を選択してください。");
            }
            return new(saved, null);
        }
        catch (OperationCanceledException) { return new(saved, "保存を中止しました。"); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        { return new(saved, "保存権限がありません。書き込み可能なフォルダーを選んでください。"); }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return new(saved, "保存できませんでした。空き容量、フォルダー名、使用状況を確認してください。リンクやジャンクションを含む保存先は利用できません。");
        }
    }
}
