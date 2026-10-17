using WinmailExtractor.Core;
using WinmailExtractor.Core.Models;
using WinmailExtractor.Core.Services;

namespace WinmailExtractor.Tests;

public sealed class SaveTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "WinmailExtractor-tests-" + Guid.NewGuid().ToString("N"));
    public SaveTests() => Directory.CreateDirectory(root);

    [Theory]
    [InlineData("..\\evil.exe", "evil.exe")]
    [InlineData("../../outside.pdf", "outside.pdf")]
    [InlineData("C:\\Users\\test\\a.txt", "a.txt")]
    [InlineData("\\\\server\\share\\b.txt", "b.txt")]
    [InlineData("a.txt:payload.exe", "a.txt_payload.exe")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("NUL", "_NUL")]
    [InlineData("COM1.pdf", "_COM1.pdf")]
    [InlineData("LPT¹.txt", "_LPT¹.txt")]
    [InlineData("report.pdf. ", "report.pdf")]
    [InlineData("\u202eevil.txt", "_evil.txt")]
    [InlineData("a\nb?.txt", "a_b_.txt")]
    [InlineData("", "attachment-001.bin")]
    [InlineData("..", "attachment-001.bin")]
    public void DangerousNamesAreFlattenedAndSavedInsideRoot(string input, string expected)
    {
        Assert.Equal(expected, FileNamePolicy.Sanitize(input));
        var result = new AttachmentSaveService().Save(root, [new(input, input, [1, 2, 3])]);
        Assert.Null(result.Error);
        string file = Assert.Single(result.SavedPaths);
        Assert.Equal(root, Path.GetDirectoryName(file));
        Assert.Equal(expected, Path.GetFileName(file));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(file));
    }
    [Fact] public void ExistingFilesAndDuplicateNamesAreNeverOverwritten()
    {
        string existing = Path.Combine(root, "見積書.pdf"); File.WriteAllBytes(existing, [9]);
        var result = new AttachmentSaveService().Save(root, [new("", "見積書.pdf", [1]), new("", "見積書.pdf", [2])]);
        Assert.Null(result.Error);
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(existing));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(Path.Combine(root, "見積書 (2).pdf")));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(Path.Combine(root, "見積書 (3).pdf")));
    }
    [Fact] public async Task ConcurrentSavesUseCreateNew()
    {
        var tasks = new List<Task<SaveResult>>();
        for (int i = 0; i < 12; i++) tasks.Add(Task.Run(() => new AttachmentSaveService().Save(root, [new("", "same.txt", [8])])));
        var results = await Task.WhenAll(tasks);
        foreach (var result in results) { Assert.Null(result.Error); Assert.Single(result.SavedPaths); }
        Assert.Equal(12, Directory.GetFiles(root).Length);
    }
    [Fact] public void VeryLongJapaneseNamesRetainExtension()
    {
        string name = string.Concat(Enumerable.Repeat("長い日本語", 80)) + ".pdf";
        var result = new AttachmentSaveService().Save(root, [new(name, name, [1])]);
        Assert.Null(result.Error);
        string path = Assert.Single(result.SavedPaths);
        Assert.EndsWith(".pdf", path);
        Assert.True(Path.GetFileName(path).Length <= 160);
    }
    [Fact] public void LockedExistingFileIsNotOverwritten()
    {
        using var locked = new FileStream(Path.Combine(root, "locked.txt"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var result = new AttachmentSaveService().Save(root, [new("", "locked.txt", [1])]);
        Assert.Null(result.Error);
        Assert.EndsWith("locked (2).txt", Assert.Single(result.SavedPaths));
    }
    [Fact] public void MissingDestinationReturnsControlledError()
    {
        var result = new AttachmentSaveService().Save(Path.Combine(root, "absent"), [new("", "x", [1])]);
        Assert.NotNull(result.Error); Assert.Empty(result.SavedPaths);
    }
    [Fact] public void DestinationCannotBeMovedWhileGuarded()
    {
        using var guard = DirectoryGuard.Acquire(root);
        try { Assert.Throws<IOException>(() => Directory.Move(root, root + "-moved")); }
        finally { if (!Directory.Exists(root) && Directory.Exists(root + "-moved")) Directory.Move(root + "-moved", root); }
    }
    [Fact] public void DirectoryLinkIsRejected()
    {
        // Junctions do not require Developer Mode/admin rights; create only in our test directory.
        string target = Path.Combine(root, "target"); Directory.CreateDirectory(target);
        string link = Path.Combine(root, "link");
        var start = new System.Diagnostics.ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("New-Item -ItemType Junction -Path '" + link.Replace("'", "''") + "' -Target '" + target.Replace("'", "''") + "' -ErrorAction Stop | Out-Null");
        using var process = System.Diagnostics.Process.Start(start)!;
        process.WaitForExit(); Assert.Equal(0, process.ExitCode);
        try
        {
            var result = new AttachmentSaveService().Save(link, [new("", "outside.txt", [1])]);
            Assert.NotNull(result.Error); Assert.Empty(Directory.GetFiles(target));
        }
        finally { Directory.Delete(link); }
    }
    [Fact] public void ExecutableAndScriptExtensionsHaveCaution()
    {
        foreach (string name in new[] { "x.EXE", "x.ps1", "x.lnk", "x.docm", "x.cmd" }) Assert.True(FileNamePolicy.RequiresCaution(name));
    }
    public void Dispose() => Directory.Delete(root, true);
}
