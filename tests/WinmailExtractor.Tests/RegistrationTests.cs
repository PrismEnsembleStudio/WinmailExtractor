using WinmailExtractor.Core.Services;
using Microsoft.Win32;

namespace WinmailExtractor.Tests;

public sealed class RegistrationTests
{
    [Fact] public void RegistrationRoundTripInIsolatedRegistryDoesNotTouchSiblings()
    {
        string sandbox = @"Software\WinmailExtractor.Tests-" + Guid.NewGuid().ToString("N");
        try
        {
            using var root = Registry.CurrentUser.CreateSubKey(sandbox, true);
            using var sibling = root.CreateSubKey("OtherApp"); sibling.SetValue("", "keep");
            var service = new ContextMenuRegistrationService(root, "WinmailExtractor", () => { });
            string executable = Environment.ProcessPath!;
            Assert.Equal(RegistrationState.NotRegistered, service.GetStatus(executable).State);
            service.Register(executable);
            Assert.Equal(RegistrationState.Registered, service.GetStatus(executable).State);
            Assert.Equal(RegistrationState.NeedsRegistration, service.GetStatus(@"C:\moved\WinmailExtractor.exe").State);
            using (var command = root.OpenSubKey(@"WinmailExtractor\command")) Assert.Equal(ContextMenuRegistrationService.BuildCommand(executable), command!.GetValue(""));
            service.Unregister(executable);
            Assert.Equal(RegistrationState.NotRegistered, service.GetStatus(executable).State);
            Assert.Equal("keep", sibling.GetValue(""));
            using (var foreign = root.CreateSubKey("WinmailExtractor")) foreign.SetValue("", "foreign");
            Assert.Equal(RegistrationState.ForeignKey, service.GetStatus(executable).State);
            Assert.Throws<InvalidOperationException>(() => service.Register(executable));
            Assert.Throws<InvalidOperationException>(() => service.Unregister(executable));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(sandbox, false); }
    }
    [Fact] public void CommandQuotesJapaneseSpacesAndSpecialCharacters()
    {
        const string path = @"C:\日本語 & portable (1)\WinmailExtractor.exe";
        Assert.Equal($"\"{path}\" \"%1\"", ContextMenuRegistrationService.BuildCommand(path));
    }
    [Theory] [InlineData("relative.exe")] [InlineData("C:\\a\"b.exe")] [InlineData("C:\\%1\\test.exe")] [InlineData("C:\\test.dll")]
    public void InvalidExecutablePathsAreRejected(string path) => Assert.Throws<ArgumentException>(() => ContextMenuRegistrationService.BuildCommand(path));
    [Fact] public void MovedExecutableRequiresReregistration()
    {
        string current = Environment.ProcessPath!;
        Assert.Equal(RegistrationState.Registered, ContextMenuRegistrationService.Evaluate(current, current, ContextMenuRegistrationService.BuildCommand(current)).State);
        Assert.Equal(RegistrationState.NeedsRegistration, ContextMenuRegistrationService.Evaluate(current, @"C:\old\WinmailExtractor.exe", "old").State);
        Assert.Equal(RegistrationState.NeedsRegistration, ContextMenuRegistrationService.Evaluate(current, current, "altered").State);
    }
}
