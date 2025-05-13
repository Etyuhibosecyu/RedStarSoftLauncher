using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;

namespace RedStarSoftLauncher.Views;
public partial class UpdateView : UserControl
{
	public UpdateView() => InitializeComponent();

	public void DownloadUpdate(HttpClient client, Uri uri, string filename, string? checksum, long start, long contentLength)
	{
		try
		{
			client.DefaultRequestHeaders.Range = new(start, contentLength);
			using var stream = client.GetStreamAsync(uri).Result;
			var bytesLeft = stream.GetType()?.GetField("_contentBytesRemaining", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(stream) is ulong ul ? ul : throw new InvalidOperationException();
			var bytes = new byte[1048576];
			using FileStream fs = new(filename + ".tmp", FileMode.Append, FileAccess.Write);
			Dispatcher.UIThread.InvokeAsync(() => (SetUpdateProgress(0), SetUpdateMaximum(contentLength))).Wait();
			while (bytesLeft > (ulong)bytes.Length)
			{
				stream.ReadExactly(bytes);
				fs.Write(bytes, 0, bytes.Length);
				bytesLeft -= (ulong)bytes.Length;
				Dispatcher.UIThread.InvokeAsync(() => SetUpdateProgress((double)contentLength - bytesLeft)).Wait();
			}
			bytes = new byte[(int)bytesLeft];
			stream.ReadExactly(bytes);
			fs.Write(bytes, 0, bytes.Length);
			stream.Dispose();
			fs.Dispose();
			ExecuteScript(client, uri, filename, checksum, contentLength);
		}
		catch
		{
			Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "A serious error has occurred while trying to start the launcher. Check your Internet connection and/or retry later. If the problem persists, contact the app developers.", ButtonEnum.Ok).ShowAsync()).Wait();
			Environment.Exit(0);
		}
	}

	public void ExecuteScript(HttpClient client, Uri uri, string filename, string? checksum, long contentLength)
	{
		try
		{
			if (!(checksum?.Equals(Convert.ToHexString(SHA512.HashData(File.ReadAllBytes(filename + ".tmp"))), StringComparison.OrdinalIgnoreCase) ?? false))
			{
				if (Dispatcher.UIThread.InvokeAsync(async () =>
					await MessageBoxManager.GetMessageBoxStandard("", "The new launcher version file has been corrupted. Press OK to download again or Cancel to exit.", ButtonEnum.OkCancel).ShowAsync()).Result == ButtonResult.Ok)
				{
					File.Delete(filename + ".tmp");
					new Thread(() => DownloadUpdate(client, uri, filename, checksum, 0, contentLength)) { Name = "Downloading", IsBackground = true }.Start();
					return;
				}
				else
					Environment.Exit(0);
			}
			var script = filename + (OperatingSystem.IsWindows() ? ".bat" : OperatingSystem.IsLinux() ? ".sh" : throw new InvalidOperationException());
			File.WriteAllText(script, OperatingSystem.IsWindows() ? $"""
				@echo off
				:not_ended
				TASKLIST | FINDSTR {Process.GetCurrentProcess().ProcessName}>NUL && goto :not_ended
				move "{filename}.tmp" "{Path.GetFileName(filename)}"
				start "" "{filename}"
				""" : OperatingSystem.IsLinux() ? $"""
				while $(pidof -s {Process.GetCurrentProcess().ProcessName}>/dev/null)
				do
					continue
				done
				mv -f "{filename}.tmp" "{filename}"
				chmod +x "{filename}"
				"{filename}" & exit
				""".Replace("\r\n", "\n") : throw new InvalidOperationException());
			if (OperatingSystem.IsWindows())
				Process.Start(script);
			else if (OperatingSystem.IsLinux())
			{
				Process.Start("chmod", "+x \"" + script + "\"").WaitForExit();
				Process.Start("/bin/bash", "-c \"nohup \"\"" + script + "\"\" &\"");
			}
			Environment.Exit(0);
			client.Dispose();
		}
		catch (Exception ex)
		{
			Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "A serious error has occurred while trying to start the launcher. Check your Internet connection and/or retry later. If the problem persists, contact the app developers." + ex.Message, ButtonEnum.Ok).ShowAsync()).Wait();
			Environment.Exit(0);
		}
	}

	private double SetUpdateMaximum(double value) => UpdateProgress.Maximum = value;

	private double SetUpdateProgress(double value) => UpdateProgress.Value = value;
}
