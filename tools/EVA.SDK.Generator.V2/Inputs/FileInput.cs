using EVA.API.Spec;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Inputs;

internal class FileInput : IInput
{
  private readonly string _path;

  internal FileInput(string path)
  {
    _path = path;
  }

  public async Task<ApiDefinitionModel> Read(ILogger logger)
  {
    if (!File.Exists(_path)) throw new SdkException($"Cannot find file: {_path}");

    logger.LogInformation("Reading spec from: {path}", _path);
    await using var file = File.OpenRead(_path);

    var result = await JsonContext.Default.ApiDefinitionModel.DeserializeAsync(file);
    if (result == null) throw new SdkException($"Could not parse file: {_path}");

    return result;
  }
}