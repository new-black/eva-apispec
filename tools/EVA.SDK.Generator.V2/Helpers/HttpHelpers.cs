using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace EVA.SDK.Generator.V2.Helpers;

public static class HttpHelpers
{
  private static readonly HttpClient _http = new HttpClient
  {
    DefaultRequestHeaders =
    {
      { "User-Agent", HttpConstants.UserAgent }
    }
  };

  internal static async ValueTask<T?> GetJson<T>(string url, JsonTypeInfo<T> info)
  {
    await using var stream = await _http.GetStreamAsync(url);
    return await JsonSerializer.DeserializeAsync(stream, info);
  }

  internal static async ValueTask GetToFile(string url, string path)
  {
    var response = await _http.GetStreamAsync(url);
    await using var target = File.OpenWrite(path);
    await response.CopyToAsync(target);
    await target.FlushAsync();
    target.Close();
  }
}