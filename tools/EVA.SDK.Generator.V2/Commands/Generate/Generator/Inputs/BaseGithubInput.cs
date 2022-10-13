using System.Text.Json;
using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;

public abstract class BaseGithubInput : IInput
{
  protected abstract string GetUrl();

  public async Task<ApiDefinitionModel> Read()
  {
    var url = GetUrl();

    using var http = new HttpClient();
    await using var stream = await http.GetStreamAsync(HttpConstants.LatestSpecUrl);
    var result = await JsonSerializer.DeserializeAsync<ApiDefinitionModel>(stream, NonIndentedSerializationHelper.Default.ApiDefinitionModel);
    if (result == null) throw new Exception($"Could not download from: {url}");
    return result;
  }
}