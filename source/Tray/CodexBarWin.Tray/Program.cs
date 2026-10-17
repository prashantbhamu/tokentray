using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CodexBarWin.Tray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (Diagnostics.TryRun(args)) return;

        // Branch builds are always isolated; never contend with the installed main widget.
        bool preview = true;
        bool open = preview || Array.IndexOf(args, "--open") >= 0;
        string prefix = "Local\\CodexBarWin.ProviderFixPreview";
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, prefix + ".Show");
        using var quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, prefix + ".Quit");
        if (Array.IndexOf(args, "--quit") >= 0) { quitSignal.Set(); return; }
        using var instance = new Mutex(true, prefix, out bool firstInstance);
        if (!firstInstance) { if (open) showSignal.Set(); return; }
        try
        {
            using var context = new TrayAppContext(open, preview);
            using var signalTimer = new System.Windows.Forms.Timer { Interval = 250 };
            signalTimer.Tick += (_, _) =>
            {
                if (quitSignal.WaitOne(0)) context.ExitThread();
                else if (showSignal.WaitOne(0)) context.ShowPopup();
            };
            signalTimer.Start();
            Application.Run(context);
        }
        finally { instance.ReleaseMutex(); }
    }

    private static void LogCrash(Exception? exception)
    {
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexBarWin", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".previous", overwrite: true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
