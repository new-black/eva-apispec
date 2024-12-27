using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EVA.SDK.Generator.V2.Exceptions;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Helpers;

internal static class HttpHelpers
{
  private static readonly HttpClient Http = new()
  {
    DefaultRequestHeaders =
    {
      { "User-Agent", HttpConstants.UserAgent }
    }
  };

  internal static async ValueTask<T> GetJson<T>(string url, JsonTypeInfo<T> info, ILogger logger)
  {
    using var response = await Http.GetAsync(url);
    if (!response.IsSuccessStatusCode) throw new NonSuccessStatusCodeException(response);
    await using var stream = await response.Content.ReadAsStreamAsync();
    var result = await JsonSerializer.DeserializeAsync(stream, info);
    if (result == null) throw new ResponseParsingFailedException(url);
    logger.LogDebug("Downloaded {Size} from {Url}", StringHelpers.FormatSize(stream.Position), url);
    return result;
  }

  internal static async ValueTask GetToFile(string url, string path, ILogger logger)
  {
    var response = await Http.GetStreamAsync(url);
    await using var target = File.OpenWrite(path);
    await response.CopyToAsync(target);
    await target.FlushAsync();
    target.Close();

    logger.LogDebug("Downloaded {Size} from {Url}", StringHelpers.FormatSize(new FileInfo(path).Length), url);
  }
}