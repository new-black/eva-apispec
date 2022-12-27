using EVA.API.Spec;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Inputs;

internal class FileInput : IInput
{
  private readonly string _path;

  internal FileInput(string path)
  {
    _path = path;
  }

  public async Task<ApiDefinitionModel> Read(bool quiet = false)
  {
    if (!File.Exists(_path)) throw new SdkException($"Cannot find file: {_path}");

    if(!quiet) Console.WriteLine($"Reading from: {_path}");
    await using var file = File.OpenRead(_path);

    var result = await JsonContext.Default.ApiDefinitionModel.DeserializeAsync(file);
    if (result == null) throw new SdkException($"Could not parse file: {_path}");

    return result;
  }
}