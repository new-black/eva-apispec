using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EVA.SDK.Generator.V2.Exceptions;

namespace EVA.SDK.Generator.V2.Helpers;

public static class HttpHelpers
{
  private static readonly HttpClient _http = new()
  {
    DefaultRequestHeaders =
    {
      { "User-Agent", HttpConstants.UserAgent }
    }
  };

  internal static async ValueTask<T> GetJson<T>(string url, JsonTypeInfo<T> info)
  {
    using var response = await _http.GetAsync(url);
    if (!response.IsSuccessStatusCode) throw new NonSuccessStatusCodeException(response);
    await using var stream = await response.Content.ReadAsStreamAsync();
    var result = await JsonSerializer.DeserializeAsync(stream, info);
    if (result == null) throw new UnparsableResponseException(url);
    return result;
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