using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinmailExtractor.Core.Services;

internal sealed class DirectoryGuard : IDisposable
{
    private readonly List<SafeFileHandle> handles = [];

    public static DirectoryGuard Acquire(string path)
    {
        var guard = new DirectoryGuard();
        try
        {
            var chain = new Stack<string>();
            for (DirectoryInfo? directory = new(path); directory != null; directory = directory.Parent)
                chain.Push(directory.FullName);
            while (chain.Count > 0)
            {
                string directory = chain.Pop();
                // Zero desired access is a metadata-only handle and does not pin rename on Windows.
                var handle = CreateFile(directory, 0x80000000 /* GENERIC_READ */, 3 /* read/write sharing, no delete */, IntPtr.Zero, 3, 0x02200000 /* backup semantics + open reparse point */, IntPtr.Zero);
                if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
                guard.handles.Add(handle);
                if (!GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if ((info.Attributes & 0x400) != 0) throw new IOException("リンクを含む保存先は利用できません。");
            }
            return guard;
        }
        catch { guard.Dispose(); throw; }
    }

    public void Dispose() { foreach (var handle in handles) handle.Dispose(); }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
}
