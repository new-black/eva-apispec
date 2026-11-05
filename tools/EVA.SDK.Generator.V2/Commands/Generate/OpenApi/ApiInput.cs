using System.Net;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi;

/// <summary>
/// Reads the OpenAPI document of EVA, from an EVA environment or a file.
/// </summary>
internal static class ApiInput
{
  private const string DocumentPath = "/openapi.json";

  internal static async Task<EvaApi> Read(GenerateOptions opt, ILogger logger)
  {
    if (opt is { Token: not null, OpenApi: true })
    {
      throw new SdkException("Use either --token to download the OpenAPI document of an EVA environment, or --openapi to read it from a file");
    }

    using var content = opt.OpenApi ? ReadFile(opt.Input, logger) : await Download(opt.Input, opt.Token!, logger);

    var result = await OpenApiDocument.LoadAsync(content, OpenApiConstants.Json);
    if (result.Diagnostic?.Errors is { Count: > 0 } errors)
    {
      throw new SdkException($"The OpenAPI document is invalid: {string.Join(", ", errors.Select(e => e.Message))}");
    }

    return EvaApi.From(result.Document ?? throw new SdkException($"Could not read the OpenAPI document from {opt.Input}"));
  }

  /// <param name="path">The path of the document, eg ./openapi/openapi.json.</param>
  /// <param name="logger">The logger.</param>
  private static Stream ReadFile(string? path, ILogger logger)
  {
    if (path == null || !File.Exists(path))
    {
      throw new SdkException("--openapi requires the path of an OpenAPI document as input (--in)");
    }

    logger.LogInformation("Reading OpenAPI document from: {Path}", path);
    return File.OpenRead(path);
  }

  /// <param name="url">The URL of the environment, eg https://api.euw.acme.test.eva-online.cloud.</param>
  /// <param name="token">A token of a user with the ViewApiSpecification functionality.</param>
  /// <param name="logger">The logger.</param>
  private static async Task<Stream> Download(string? url, string token, ILogger logger)
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

    var content = new MemoryStream();
    await response.Content.CopyToAsync(content);
    logger.LogDebug("Downloaded {Size}", StringHelpers.FormatSize(content.Length));
    content.Position = 0;
    return content;
  }
}
