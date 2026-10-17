using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using CodexBarWin.Core;
using CodexBarWin.Core.Models;

namespace CodexBarWin.Tray;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		int statusIndex = Array.IndexOf(args, "--status");
		if (statusIndex >= 0 && statusIndex + 1 < args.Length)
		{
			var registry = new ProviderRegistry(new CodexBarWin.Core.IProvider[] {
				new CodexBarWin.Core.Providers.CodexProvider(new CodexBarWin.Core.Providers.CodexUsageClient()),
				new CodexBarWin.Core.Providers.ClaudeProvider(new CodexBarWin.Core.Providers.ClaudeUsageClient()) });
			File.WriteAllText(args[statusIndex + 1], System.Text.Json.JsonSerializer.Serialize(registry.RefreshAll(),
				new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
			return;
		}
		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += delegate(object _, ThreadExceptionEventArgs e)
		{
			CrashLog("ThreadException", e.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs e)
		{
			CrashLog("UnhandledException", e.ExceptionObject as Exception);
		};
		Application.SetHighDpiMode(HighDpiMode.SystemAware);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		int num = Array.IndexOf(args, "--shot");
		if (num >= 0 && num + 1 < args.Length)
		{
			RunCapture(args[num + 1], CapturePopup);
			return;
		}
		int num2 = Array.IndexOf(args, "--shot-settings");
		if (num2 >= 0 && num2 + 1 < args.Length)
		{
			RunCapture(args[num2 + 1], delegate(string p)
			{
				CaptureForm(new SettingsForm(SettingsStore.Load()), p);
			});
			return;
		}
		int num3 = Array.IndexOf(args, "--shot-icon");
		if (num3 >= 0 && num3 + 1 < args.Length)
		{
			RunCapture(args[num3 + 1], CaptureIcon);
		}
		else
		{
			Application.Run(new TrayAppContext(Array.IndexOf(args, "--open") >= 0));
		}
	}

	private static void CrashLog(string kind, Exception? ex)
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexBarWin", "crash.log");
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.AppendAllText(path, $"{DateTimeOffset.Now:O} {kind}: {ex}{Environment.NewLine}{Environment.NewLine}");
		}
		catch
		{
		}
	}

	private static void RunCapture(string path, Action<string> capture)
	{
		try
		{
			capture(path);
		}
		catch (Exception ex)
		{
			File.WriteAllText(path + ".err", ex.ToString());
		}
	}

	private static void CapturePopup(string path)
	{
		IReadOnlyList<UsageSnapshot> snapshots = ProviderRegistry.CreateDefault().RefreshAll();
		using PopupForm popupForm = new PopupForm(delegate
		{
		});
		popupForm.Render(snapshots);
		popupForm.Location = new Point(-4000, -4000);
		popupForm.Show();
		Application.DoEvents();
		popupForm.PerformLayout();
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "--preview-dark") >= 0) Theme.Preview(false);
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "--preview-light") >= 0) Theme.Preview(true);
		using Bitmap bitmap = new Bitmap(popupForm.Width, popupForm.Height);
		popupForm.DrawToBitmap(bitmap, new Rectangle(0, 0, popupForm.Width, popupForm.Height));
		bitmap.Save(path, ImageFormat.Png);
		popupForm.Close();
	}

	private static void CaptureIcon(string path)
	{
		double[] array = new double[3] { 0.07, 0.55, 0.92 };
		using Bitmap bitmap = new Bitmap(array.Length * 56 + 8, 64);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.Clear(Color.FromArgb(40, 40, 48));
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			int num = 8;
			double[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				nint handle;
				Icon icon = TrayIconRenderer.Build(array2[i], hasData: true, out handle);
				using (Bitmap image = icon.ToBitmap())
				{
					graphics.DrawImage(image, new Rectangle(num, 8, 48, 48));
				}
				icon.Dispose();
				TrayIconRenderer.Destroy(handle);
				num += 56;
			}
		}
		bitmap.Save(path, ImageFormat.Png);
	}

	private static void CaptureForm(Form form, string path)
	{
		form.Location = new Point(-4000, -4000);
		form.Show();
		Application.DoEvents();
		using Bitmap bitmap = new Bitmap(form.Width, form.Height);
		form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
		bitmap.Save(path, ImageFormat.Png);
		form.Close();
	}
}
