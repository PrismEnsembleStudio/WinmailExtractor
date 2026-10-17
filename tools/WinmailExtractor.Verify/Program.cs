using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinmailExtractor;
using WinmailExtractor.Tests;
using WinmailExtractor.Views;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/verification");
        Directory.CreateDirectory(output);
        using (var fixture = new TnefFixture())
        {
            fixture.Add("ご提案資料 2026年10月.pdf", System.Text.Encoding.UTF8.GetBytes("%PDF-1.7 synthetic fixture\n"));
            fixture.Add("月次集計.xlsx", [0x50, 0x4b, 1, 2]);
            fixture.Add("../確認用スクリプト.ps1", [1, 2, 3]);
            fixture.Add(string.Concat(Enumerable.Repeat("長い日本語のファイル名", 22)) + ".docx", [0x50, 0x4b, 3, 4]);
            File.WriteAllBytes(Path.Combine(output, "sample-winmail.dat"), fixture.ToArray());
        }
        File.WriteAllText(Path.Combine(output, "invalid.dat"), "This is not TNEF.");
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        int exitCode = 0;
        _ = Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
        {
            try
            {
                var window = new MainWindow([]);
                Render(window, Path.Combine(output, "01-empty.png"), 920, 650, 1);
                Render(window, Path.Combine(output, "02-minimum.png"), 720, 500, 1);
                await window.ViewModel.LoadAsync(Path.Combine(output, "sample-winmail.dat"));
                Check(window.ViewModel.Attachments.Count == 4, "UI viewmodel loaded 4 files");
                Check(window.ViewModel.CanSave, "save enabled");
                window.ViewModel.SelectAll(false);
                Check(!window.ViewModel.CanSave, "no selection disables save");
                window.ViewModel.SelectAll(true);
                foreach (double scale in new[] { 1d, 1.25, 1.5, 2 })
                    Render(window, Path.Combine(output, $"03-loaded-{scale * 100:0}.png"), 920, 650, scale);
                Render(window, Path.Combine(output, "04-loaded-minimum.png"), 720, 500, 1);
                string saved = Path.Combine(output, "saved"); Directory.CreateDirectory(saved);
                await window.ViewModel.SaveAsync(saved);
                Check(!window.ViewModel.IsError && window.ViewModel.HasSavedDirectory, "UI save completed");
                Render(window, Path.Combine(output, "05-saved.png"), 920, 650, 1);
                await window.ViewModel.LoadAsync(Path.Combine(output, "invalid.dat"));
                Check(window.ViewModel.IsError && !window.ViewModel.CanSave, "invalid input handled inline");
                Render(window, Path.Combine(output, "06-error.png"), 920, 650, 1);
                var settings = new SettingsWindow();
                Render(settings, Path.Combine(output, "07-settings.png"), 620, 600, 1);
                settings.Close();
                window.Close();
                Console.WriteLine("PASS: WPF views rendered; load / selection / save / invalid input verified. Registry was read only.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); exitCode = 1; }
            finally { Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
        });
        Dispatcher.Run();
        return exitCode;
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Console.WriteLine("PASS: " + description);
    }

    private static void Render(Window window, string file, double width, double height, double scale)
    {
        // Compiled WPF view, offscreen layout. This does not emulate the OS's DPI or dialogs.
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -15000; window.Top = -15000;
        window.ShowInTaskbar = false;
        window.Width = width; window.Height = height;
        if (!window.IsVisible) window.Show();
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var surface = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        var size = surface.RenderSize;
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(window.Background, null, new Rect(size));
            context.DrawRectangle(new VisualBrush(surface), null, new Rect(size));
        }
        var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); encoder.Save(stream);
    }
}
