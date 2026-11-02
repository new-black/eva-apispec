using System.Net;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi;

/// <summary>
/// Downloads the OpenAPI document of an EVA environment.
/// </summary>
internal static class ApiInput
{
  private const string DocumentPath = "/openapi.json";

  /// <param name="url">The URL of the environment, eg https://api.euw.acme.test.eva-online.cloud.</param>
  /// <param name="token">A token of a user with the ViewApiSpecification functionality.</param>
  /// <param name="logger">The logger.</param>
  internal static async Task<EvaApi> Read(string? url, string token, ILogger logger)
  {
    if (url == null || !(url.StartsWith("https://") || url.StartsWith("http://")))
    {
      throw new SdkException("--token requires the URL of an EVA environment as input (--in)");
    }

    var documentUrl = new Uri(new Uri(url), DocumentPath);
    logger.LogInformation("Downloading OpenAPI document from: {Url}", documentUrl);

    using var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip });
    using var request = new HttpRequestMessage(HttpMethod.Get, documentUrl);
    request.Headers.Add("EVA-User-Agent", HttpConstants.UserAgent);

    // A token without scheme is sent as a bearer token
    request.Headers.TryAddWithoutValidation("Authorization", token.Contains(' ') ? token : $"Bearer {token}");

    using var response = await http.SendAsync(request);
    if (!response.IsSuccessStatusCode) throw new NonSuccessStatusCodeException(response);

    using var content = new MemoryStream();
    await response.Content.CopyToAsync(content);
    logger.LogDebug("Downloaded {Size}", StringHelpers.FormatSize(content.Length));
    content.Position = 0;

    var result = OpenApiDocument.Load(content, OpenApiConstants.Json);
    if (result.Diagnostic?.Errors is { Count: > 0 } errors)
    {
      throw new SdkException($"The OpenAPI document is invalid: {string.Join(", ", errors.Select(e => e.Message))}");
    }

    return EvaApi.From(result.Document ?? throw new ResponseParsingFailedException(documentUrl.ToString()));
  }
}
