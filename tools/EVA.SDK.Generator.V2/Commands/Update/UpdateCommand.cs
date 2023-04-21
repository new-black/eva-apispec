using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Update;

internal static class UpdateCommand
{
  private static readonly Option<bool> WaitOption = new(name: "--wait") { IsHidden = true };

  internal static void Register(Command command)
  {
    var updateCommand = new Command("update");
    updateCommand.AddOption(WaitOption);
    command.Add(updateCommand);

    updateCommand.SetHandler(async (wait, logger) =>
    {
      // Locations
      var currentExeLocation = Environment.ProcessPath;
      if (currentExeLocation == null) throw new SdkException("Could not determine current executable location");

      if (wait)
      {
        await TrySwitchExecutables(currentExeLocation);
      }
      else
      {
        await DownloadNewExecutable(currentExeLocation, logger);
      }
    }, WaitOption, LogBinder.Instance);
  }

  private static async Task DownloadNewExecutable(string currentExeLocation, ILogger logger)
  {
    var newExeLocation = PathInSameFolder(currentExeLocation, static p => $"new-{p}");
    var entryAssembly = Assembly.GetEntryAssembly();
    if (entryAssembly == null) throw new SdkException("Could not determine entry assembly");

    // Find current version
    var version = entryAssembly.GetName().Version?.ToString(3);
    if (version == null) throw new SdkException("Could not determine current version");
    logger.LogInformation("Current version: {CurrentVersion}", version);

    // Find latest version
    var response = await HttpHelpers.GetJson(HttpConstants.LatestReleaseTage, JsonContext.Default.LatestResponse, logger);
    var latestVersion = response.TagName;
    if (latestVersion == null || !latestVersion.StartsWith(HttpConstants.TagPrefix))
    {
      throw new SdkException("Could not determine latest version, update cancelled");
    }

    latestVersion = latestVersion[HttpConstants.TagPrefix.Length..];
    logger.LogInformation("Latest version: {LatestVersion}", latestVersion);

    // Check for up-to-date
    if (version == latestVersion)
    {
      logger.LogInformation("Application already up-to-date");
      return;
    }

    logger.LogInformation("Trying to update {CurrentVersion} -> {LatestVersion}", version, latestVersion);

    // Find the correct asset
    var expectedAssetName = $"sdk-generator-{GetRuntimeIdentifier()}{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "")}";
    var asset = response.Assets?.FirstOrDefault(a => a.Name == expectedAssetName && a.BrowserDownloadUrl != null);
    var browserDownloadUrl = asset?.BrowserDownloadUrl;
    if (asset == null || browserDownloadUrl == null) throw new SdkException($"Cannot find asset: {expectedAssetName} for version {latestVersion}");

    // Download the asset
    await HttpHelpers.GetToFile(browserDownloadUrl, newExeLocation, logger);

    // Start child process for replacement
    Process.Start(new ProcessStartInfo
    {
      WorkingDirectory = Directory.GetCurrentDirectory(),
      FileName = newExeLocation,
      Arguments = "update --wait"
    });
  }

  private static async Task TrySwitchExecutables(string currentExeLocation)
  {
    var newExeLocation = PathInSameFolder(currentExeLocation, static p => p[4..]);

    // Try 10 times
    foreach (var _ in Enumerable.Range(0, 10))
    {
      try
      {
        if (File.Exists(newExeLocation)) File.Delete(newExeLocation);
        File.Move(currentExeLocation, newExeLocation);
        Console.WriteLine("Update completed!");
        break;
      }
      catch
      {
        // Ignore
      }

      await Task.Delay(TimeSpan.FromMilliseconds(250));
    }
  }

  private static string PathInSameFolder(string file, Func<string, string> transformFileName)
  {
    var directory = Path.GetDirectoryName(file);
    directory ??= string.Empty;
    return Path.Combine(directory, transformFileName(Path.GetFileName(file)));
  }

  private static string GetRuntimeIdentifier()
  {
    var arch = RuntimeInformation.ProcessArchitecture;
    string os;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      os = "win";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
      os = "linux";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      os = "osx";
    }
    else
    {
      throw new SdkException("Unsupported OS");
    }

    return (arch, os) switch
    {
      (Architecture.X64, "win") => "win-x64",
      (Architecture.X64, "linux") => "linux-x64",
      (Architecture.X64, "osx") => "osx-x64",
      _ => throw new SdkException("Unsupported OS")
    };
  }

  public class LatestResponse
  {
    [JsonPropertyName("assets")] public List<Asset>? Assets { get; set; }

    [JsonPropertyName("tag_name")] public string? TagName { get; set; }

    public class Asset
    {
      [JsonPropertyName("name")] public string? Name { get; set; }

      [JsonPropertyName("browser_download_url")]
      public string? BrowserDownloadUrl { get; set; }
    }
  }
}