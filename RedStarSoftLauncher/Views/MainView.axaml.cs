using Avalonia.Threading;
using HtmlAgilityPack;
using Jint;
using Microsoft.Win32;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace RedStarSoftLauncher.Views;

public partial class MainView : UserControl
{
#pragma warning disable IDE0079 // Удалить ненужное подавление
#pragma warning disable SYSLIB1054
	[DllImport("shell32", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
	private static extern string SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, nint hToken = 0);
#pragma warning restore SYSLIB1054
#pragma warning restore IDE0079 // Удалить ненужное подавление
	private static readonly Engine engine = new();
	private const string site = "https://red-star-soft.com";

	public MainView()
	{
		InitializeComponent();
		_ = 0;
	}

	private void UserControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CheckForUpdates().Wait();

	private async Task CheckForUpdates()
	{
#if DEBUG
		return;
#pragma warning disable CS0162 // Обнаружен недостижимый код
#endif
		try
		{
			var filename = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException();
			if (File.Exists(filename + ".bat"))
			{
				Process.Start("taskkill", "/im \"" + filename + ".bat \" /f").WaitForExit();
				File.Delete(filename + ".bat");
			}
			if (File.Exists(filename + ".sh"))
			{
				Process.Start("/bin/bash", "-c \"killall -9 \"\"" + filename + ".sh\"\">/dev/null\"").WaitForExit();
				File.Delete(filename + ".sh");
			}
			var newfilename = filename + ".tmp";
			var executableName = Path.GetFileName(filename);
			Uri uri = new(site + "/autoinstaller.php");
			var client = new HttpClient(new HttpClientHandler() { UseDefaultCredentials = true });
			client.DefaultRequestHeaders.Add("User-Agent",
				OperatingSystem.IsWindows() ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0" : OperatingSystem.IsLinux() ? "Mozilla/5.0 (X11; Linux x86_64; rv:125.0) Gecko/20100101 Firefox/125.0" : throw new InvalidOperationException());
			using var headers = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).Result;
			var start = File.Exists(newfilename) ? new FileInfo(newfilename).Length : 0;
			var checksum = headers.Headers.GetValues("Content-Checksum").FirstOrDefault();
			if (checksum?.Equals(Convert.ToHexString(SHA512.HashData(File.ReadAllBytes(filename))), StringComparison.OrdinalIgnoreCase) ?? false)
				return;
			if (!int.TryParse(headers.Headers.GetValues("Content-Modified").FirstOrDefault(), out var modified))
				throw new InvalidOperationException();
			//if (new FileInfo(filename).LastWriteTimeUtc.Subtract(DateTime.UnixEpoch).TotalSeconds >= modified)
			//	return;
			var contentLength = headers.Content.Headers.ContentLength ?? throw new InvalidOperationException();
			MainPanel.IsVisible = false;
			UpdateView.IsVisible = true;
			if (start >= contentLength)
				new Thread(() => UpdateView.ExecuteScript(client, uri, filename, checksum, contentLength)) { Name = "Downloading", IsBackground = true }.Start();
			else
				new Thread(() => UpdateView.DownloadUpdate(client, uri, filename, checksum, start, contentLength)) { Name = "Downloading", IsBackground = true }.Start();
		}
		catch
		{
			await Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "A serious error has occurred while trying to start the launcher. Check your Internet connection and/or retry later. If the problem persists, contact the app developers.", ButtonEnum.Ok).ShowAsync());
			Environment.Exit(0);
		}
#if DEBUG
#pragma warning restore CS0162 // Обнаружен недостижимый код
#endif
	}

	private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		Thread thread = new(Download) { Name = "Downloading files", IsBackground = true };
		thread.Start(sender);
	}

	private void Button_Click2(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		Thread thread = new(Download2) { Name = "Downloading files", IsBackground = true };
		thread.Start(sender);
	}

	private async void Download(object? sender)
	{
		try
		{
			HtmlWeb hw = new();
			var tag = await Dispatcher.UIThread.InvokeAsync(() => (sender as Button ?? throw new InvalidOperationException()).Tag + (OperatingSystem.IsWindows() ? "" : OperatingSystem.IsLinux() ? " Linux" : throw new InvalidOperationException()));
			var downloads = OperatingSystem.IsWindows() ? SHGetKnownFolderPath(new("374DE290-123F-4565-9164-39C4925E467B"), 0) : OperatingSystem.IsLinux() ? "." : throw new InvalidOperationException();
			var doc = hw.Load(site + "/Lineedge/" + tag + ".html");
			var hrefs = doc.DocumentNode.SelectNodes("//a[@href]").Select(x => x.Attributes["href"]?.Value ?? "").Where(x => x?.StartsWith("/download.php?file=") ?? false).ToArray();
			await Dispatcher.UIThread.InvokeAsync(() => (Total.Value = 0, Current.Value = 0, Total.Maximum = hrefs.Length));
			await DownloadInternal(tag, downloads, hrefs);
			if (OperatingSystem.IsWindows())
			{
				if (!Directory.Exists(downloads + "/Red-Star-Soft"))
					Directory.CreateDirectory(downloads + "/Red-Star-Soft");
				await LaunchOnWindows(downloads, tag.Replace(" download", ""));
			}
			else if (OperatingSystem.IsLinux())
			{
				if (OperatingSystem.IsLinux() && !Directory.Exists(downloads))
					Directory.CreateDirectory(downloads);
				if (!Directory.Exists(downloads + "/Red-Star-Soft"))
					Directory.CreateDirectory(downloads + "/Red-Star-Soft");
				if (!File.Exists(downloads + "/" + tag + "/Lineedge.sh"))
				{
					File.WriteAllBytes("./7zz", Properties.Resources._7zz);
					Process.Start("chmod", "+x ./7zz").WaitForExit();
					Process.Start("./7zz", "x \"" + downloads + "/Red-Star-Soft/" + tag + ".7z.001\" -y -o\"" + downloads + "/" + tag + "/\"").WaitForExit();
					File.Delete("./7zz");
				}
				Process.Start("chmod", "+x \"" + downloads + "/" + tag + "/Lineedge.sh\"").WaitForExit();
				Process.Start("/bin/bash", "-c \"nohup \"\"" + downloads + "/" + tag + "/Lineedge.sh\"\" &\"");
				Environment.Exit(0);
			}
		}
		catch
		{
			await Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "A serious error has occurred while trying to download the Lineedge files. Check your Internet connection and/or retry later. If the problem persists, contact the app developers.", ButtonEnum.Ok).ShowAsync());
		}
	}

	private async void Download2(object? sender)
	{
		try
		{
			HtmlWeb hw = new();
			var tag = await Dispatcher.UIThread.InvokeAsync(() => (sender as Button ?? throw new InvalidOperationException()).Tag + (OperatingSystem.IsWindows() ? "" : OperatingSystem.IsLinux() ? " Linux" : throw new InvalidOperationException()));
			var downloads = OperatingSystem.IsWindows() ? SHGetKnownFolderPath(new("374DE290-123F-4565-9164-39C4925E467B"), 0) : OperatingSystem.IsLinux() ? "." : throw new InvalidOperationException();
			var doc = hw.Load(site + "/" + tag.Replace(" Linux", "") + ".html");
			var hrefs = doc.DocumentNode.SelectNodes("//a[@href]").Select(x => x.Attributes["href"]?.Value ?? "").Where(x => (x?.StartsWith("javascript:location.replace") ?? false)
			&& (x?.Contains(OperatingSystem.IsWindows() ? ".exe" : OperatingSystem.IsLinux() ? " Linux" : throw new InvalidOperationException()) ?? false)).ToArray();
			await Dispatcher.UIThread.InvokeAsync(() => (Total.Value = 0, Current.Value = 0, Total.Maximum = hrefs.Length));
			await DownloadInternal(tag, downloads, hrefs);
			tag = tag[(tag.IndexOf('/') + 1)..];
			if (OperatingSystem.IsWindows())
			{
				if (!Directory.Exists(downloads + "/Red-Star-Soft"))
					Directory.CreateDirectory(downloads + "/Red-Star-Soft");
				await LaunchOnWindows(downloads, tag);
			}
			else if (OperatingSystem.IsLinux())
			{
				if (OperatingSystem.IsLinux() && !Directory.Exists(downloads))
					Directory.CreateDirectory(downloads);
				if (!Directory.Exists(downloads + "/Red-Star-Soft"))
					Directory.CreateDirectory(downloads + "/Red-Star-Soft");
				if (!Directory.Exists(downloads + "/" + tag))
					Directory.CreateDirectory(downloads + "/" + tag);
				string? toLaunch;
				if ((toLaunch = Directory.GetFiles(downloads + "/" + tag + "/").FirstOrDefault(x => x.EndsWith(".sh") && !x.Contains("unins"))) == null)
				{
					File.WriteAllBytes("./7zz", Properties.Resources._7zz);
					Process.Start("chmod", "+x ./7zz").WaitForExit();
					Process.Start("./7zz", "x \"" + downloads + "/Red-Star-Soft/" + tag + ".7z\" -y -o\"" + downloads + "/" + tag + "/\"").WaitForExit();
					File.Delete("./7zz");
				}
				Process.Start("chmod", "+x \"" + toLaunch + "\"").WaitForExit();
				Process.Start("/bin/bash", "-c \"nohup \"\"" + toLaunch + "\"\" &\"");
				Environment.Exit(0);
			}
		}
		catch
		{
			await Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "A serious error has occurred while trying to download the app files. Check your Internet connection and/or retry later. If the problem persists, contact the app developers.", ButtonEnum.Ok).ShowAsync());
		}
	}

	private async Task DownloadInternal(string tag, string downloads, string[] hrefs)
	{
		foreach (var href in hrefs)
		{
			var s = href;
			var foundIndex = s.IndexOf('=') + 1;
			if (OperatingSystem.IsLinux() && !Directory.Exists(downloads))
				Directory.CreateDirectory(downloads);
			if (!Directory.Exists(downloads + "/Red-Star-Soft"))
				Directory.CreateDirectory(downloads + "/Red-Star-Soft");
			var filename = downloads + "/Red-Star-Soft/" + (foundIndex == -1 ? throw new InvalidOperationException() : tag = engine.Evaluate("return decodeURI('" + s[foundIndex..] + "');").AsString());
			Uri uri = new(site + s);
			using var client = new HttpClient(new HttpClientHandler() { UseDefaultCredentials = true });
			using var headers = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
			var start = File.Exists(filename) ? new FileInfo(filename).Length : 0;
			var contentLength = headers.Content.Headers.ContentLength ?? throw new InvalidOperationException();
			if (start >= contentLength)
			{
				await Dispatcher.UIThread.InvokeAsync(() => (Current.Value = 0, Total.Value++));
				continue;
			}
			client.DefaultRequestHeaders.Range = new(start, contentLength);
			using var stream = await client.GetStreamAsync(uri);
			var bytesLeft = stream.GetType()?.GetField("_contentBytesRemaining", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(stream) is ulong ul ? ul : throw new InvalidOperationException();
			var bytes = new byte[1048576];
			using FileStream fs = new(filename, FileMode.Append, FileAccess.Write);
			await Dispatcher.UIThread.InvokeAsync(() => Current.Maximum = contentLength);
			while (bytesLeft > (ulong)bytes.Length)
			{
				stream.ReadExactly(bytes);
				fs.Write(bytes, 0, bytes.Length);
				bytesLeft -= (ulong)bytes.Length;
				await Dispatcher.UIThread.InvokeAsync(() => Current.Value = (double)contentLength - bytesLeft);
			}
			bytes = new byte[(int)bytesLeft];
			stream.ReadExactly(bytes);
			fs.Write(bytes, 0, bytes.Length);
			await Dispatcher.UIThread.InvokeAsync(() => (Current.Value = 0, Total.Value++));
		}
	}

	private static async Task LaunchOnWindows(string downloads, string tag)
	{
		var buildTags = new[] { tag, tag + " build 2", tag + " build 3", tag + " build 4", tag + " build 5", tag + " build 6", tag + " build 7", tag + " build 8", tag + " build 9", tag + " build 10" };
		string? launchName = null;
		int i;
		for (i = buildTags.Length - 1; i >= 0; i--)
		{
			var buildTag = buildTags[i];
			var (Success, Filename) = await ValidateInstallation(buildTag);
			if (Success && Filename != null)
			{
				launchName = Filename;
				break;
			}
		}
		for (i++; i < buildTags.Length; i++)
		{
			var buildTag = buildTags[i];
			var baseFileName = downloads + "/Red-Star-Soft/" + buildTag;
			Process? process;
			if (File.Exists(baseFileName + ".exe"))
				process = Process.Start(baseFileName + ".exe");
			else if (File.Exists(baseFileName + " Windows.exe"))
				process = Process.Start(baseFileName + " Windows.exe");
			else
				continue;
			process.WaitForExit();
			var (Success, Filename) = await ValidateInstallation(buildTag);
			if (!Success)
				return;
			else if (Filename != null)
				launchName = Filename;
		}
		if (launchName != null)
		{
			Process.Start(launchName);
			Environment.Exit(0);
		}
	}

	private static async Task<(bool Success, string? Filename)> ValidateInstallation(string tag)
	{
		var registry_key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
#pragma warning disable IDE0079 // Удалить ненужное подавление
#pragma warning disable CA1416
		using var key = Registry.LocalMachine.OpenSubKey(registry_key);
		if (key == null)
		{
			await Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "Error: probably your system is seriously corrupted, and in general, it's surprising how at least something works for you.", ButtonEnum.Ok).ShowAsync());
			return (false, null);
		}
		foreach (var x in key.GetSubKeyNames())
		{
			using var subkey = key.OpenSubKey(x);
			var displayName = subkey?.GetValue("DisplayName") as string;
			if (!(displayName?.Contains(tag) ?? false))
				continue;
			if (subkey?.GetValue("InstallLocation") is not string location)
				return (false, null);
			string? filename;
			if (!Process.GetProcesses().Any(x => x.ProcessName.Contains(tag) && !x.ProcessName.Contains("LineedgeLauncher"))
				&& (filename = Directory.GetFiles(location += location.EndsWith('/')
				|| location.EndsWith('\\') ? "" : "/").FirstOrDefault(x => x.EndsWith(".exe") && !x.Contains("unins"))) != null)
				return (true, filename);
			return (true, null);
#pragma warning restore CA1416
#pragma warning restore IDE0079 // Удалить ненужное подавление
		}
		return (false, null);
	}

	private void Copyrights_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CopyrightsView.IsVisible = true;
}
