using System.Text;
using System.Text.Json;
using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Generator.Inputs;

public class HttpInput : IInput
{
  private readonly string _url;

  public HttpInput(string url)
  {
    _url = url;
  }

  public async Task<ApiDefinitionModel> Read()
  {
    using var http = new HttpClient
    {
      BaseAddress = new Uri(_url),
      DefaultRequestHeaders = { { "EVA-User-Agent", "eva-sdk-generator" } }
    };

    var content = JsonSerializer.Serialize(new Request(), NonIndentedSerializationHelper.Default.Request);
    var response = await http.PostAsync("/api/core/GetApiDefinition", new StringContent(content, Encoding.UTF8, "application/json"));

    var responseModel = (await JsonSerializer.DeserializeAsync(
        await response.Content.ReadAsStreamAsync(),
        typeof(Response),
        new NonIndentedSerializationHelper(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))
      ) as Response;

    response.EnsureSuccessStatusCode();

    if (responseModel == null) throw new Exception($"Could not download from environment: {_url}.");
    return responseModel.ApiDefinition;
  }

  public class Request
  {
  }

  public class Response
  {
    public ApiDefinitionModel ApiDefinition { get; set; }
  }
}