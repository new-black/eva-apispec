using System.Text;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Inputs;

internal class HttpInput : IInput
{
  private readonly string _url;

  internal HttpInput(string url)
  {
    _url = url;
  }

  public async Task<ApiDefinitionModel> Read(ILogger logger)
  {
    logger.LogInformation("Downloading spec from: {Url}", _url);

    using var http = new HttpClient
    {
      BaseAddress = new Uri(_url),
      DefaultRequestHeaders = { { "EVA-User-Agent", HttpConstants.UserAgent } }
    };

    using var response = await http.GetAsync(HttpConstants.EvaEndpoint);
    if (!response.IsSuccessStatusCode) throw new NonSuccessStatusCodeException(response);

    var responseModel = await JsonContext.Default.ApiDefinitionModel.DeserializeAsync(await response.Content.ReadAsStreamAsync());
    if (responseModel == null) throw new ResponseParsingFailedException(_url);

    return responseModel;
  }
}