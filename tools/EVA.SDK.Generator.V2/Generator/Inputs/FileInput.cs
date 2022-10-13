using System.Text.Json;
using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Generator.Inputs;

public class FileInput : IInput
{
  private readonly string _path;

  public FileInput(string path)
  {
    _path = path;
  }

  public async Task<ApiDefinitionModel> Read()
  {
    if (!File.Exists(_path)) throw new Exception($"File {_path} does not exist");

    Console.WriteLine($"Reading from: {_path}");
    await using var file = File.OpenRead(_path);
    return await JsonSerializer.DeserializeAsync(file, NonIndentedSerializationHelper.Default.ApiDefinitionModel);
  }
}