using System.Text;
using System.Text.Json;
using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;

public class HttpInput : IInput
{
  private readonly string _url;

  public HttpInput(string url)
  {
    _url = url;
  }

  public async Task<ApiDefinitionModel> Read()
  {
    Console.WriteLine("Downloading from: " + _url);

    using var http = new HttpClient
    {
      BaseAddress = new Uri(_url),
      DefaultRequestHeaders = { { "EVA-User-Agent", HttpConstants.UserAgent } }
    };

    var response = await http.PostAsync(HttpConstants.EvaEndpoint, new StringContent("{}", Encoding.UTF8, "application/json"));

    var responseModel = (await JsonSerializer.DeserializeAsync(
        await response.Content.ReadAsStreamAsync(),
        typeof(Response),
        new NonIndentedSerializationHelper(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))
      ) as Response;

    response.EnsureSuccessStatusCode();

    if (responseModel == null) throw new Exception($"Could not download from environment: {_url}.");
    return responseModel.ApiDefinition!;
  }

  public class Response
  {
    public ApiDefinitionModel? ApiDefinition { get; set; }
  }
}