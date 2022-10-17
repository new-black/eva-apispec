using System.Text.Json;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;

public class FileInput : IInput
{
  private readonly string _path;

  public FileInput(string path)
  {
    _path = path;
  }

  public async Task<ApiDefinitionModel> Read(bool quiet = false)
  {
    if (!File.Exists(_path)) throw new Exception($"File {_path} does not exist");

    if(!quiet) Console.WriteLine($"Reading from: {_path}");
    await using var file = File.OpenRead(_path);
    return await JsonSerializer.DeserializeAsync(file, NonIndentedSerializationHelper.Default.ApiDefinitionModel);
  }
}