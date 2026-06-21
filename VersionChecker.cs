using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Glyphborn.Mapper
{
	public static class VersionChecker
	{
		public const string LOCAL_VERSION = "1.2.1";
		const string VERSION_URL = "https://raw.githubusercontent.com/DoItBetter-Studio/Mapper/main/version.txt";
		const string UPDATE_ZIP = "update.zip";

		static readonly HttpClient _client = new();

		static async Task<string> GetRemoteVersionAsync()
		{
			var text = await _client.GetStringAsync(VERSION_URL);
			return text.Trim();
		}

		public static async Task CheckForUpdatesAsync()
		{
			try
			{
				var remote = await GetRemoteVersionAsync();

				Version current = new Version(LOCAL_VERSION);
				Version version = new Version(remote);

				if (version > current)
				{
					string zipPath = await DownloadUpdateAsync(remote);
					LaunchUpdaterAndExit(zipPath);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Update check failed: {ex.Message}");
			}
		}

		static async Task<string> DownloadUpdateAsync(string version)
		{
			string url = $"https://github.com/DoItBetter-Studio/Mapper/releases/download/v{version}/Mapper-win-x64.zip";

			string exeDir = Path.GetDirectoryName(Environment.ProcessPath!)!;
			string zipPath = Path.Combine(exeDir, UPDATE_ZIP);

			var data = await _client.GetByteArrayAsync(url);
			await File.WriteAllBytesAsync(zipPath, data);

			return zipPath;
		}

		static void LaunchUpdaterAndExit(string zipPath)
		{
			string exePath = Environment.ProcessPath!;
			string exeDir = Path.GetDirectoryName(exePath)!;
			int pid = Environment.ProcessId;

			var psi = new ProcessStartInfo
			{
				FileName = Path.Combine(exeDir, OperatingSystem.IsWindows() ? "Updater.exe" : "Updater"),
				Arguments = $"{pid} \"{zipPath}\" \"{exePath}\"",
				UseShellExecute = false
			};

			Process.Start(psi);
			Environment.Exit(0);
		}
	}
}