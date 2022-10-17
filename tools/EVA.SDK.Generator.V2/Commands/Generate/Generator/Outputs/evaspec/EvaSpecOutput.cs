using System.Text.Json;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.evaspec;

public class EvaSpecOutput : IOutput
{
  private readonly EvaSpecOptions _options;

  public EvaSpecOutput(EvaSpecOptions options)
  {
    _options = options;
  }

  public void FixOptions(GenerateOptions options)
  {
    // No-op
  }

  public async Task Write(ApiDefinitionModel input, string outputDirectory)
  {
    // Main spec file
    var specFile = Path.Combine(outputDirectory, "spec.json");

    Console.WriteLine($"Writing EVA Spec file: {specFile}");
    await using var file = File.OpenWrite(specFile);
    await JsonSerializer.SerializeAsync(file, input, NonIndentedSerializationHelper.Default.ApiDefinitionModel);

    // Source files
    if (!_options.IncludeSrcFiles) return;

    // Setup dirs
    var servicesDir = Path.GetFullPath(Path.Combine(outputDirectory, "./src/services"));
    var typesDir = Path.GetFullPath(Path.Combine(outputDirectory, "./src/types"));
    Directory.CreateDirectory(servicesDir);
    Directory.CreateDirectory(typesDir);

    // Write things
    foreach (var service in input.Services)
    {
      var servicePath = Path.GetFullPath(Path.Combine(servicesDir, $"./{service.Name}.json"));
      await using var serviceFile = File.OpenWrite(servicePath);
      await JsonSerializer.SerializeAsync(serviceFile, service, IndentedSerializationHelper.Default.ServiceModel);
    }
    foreach (var (id, type) in input.Types)
    {
      var typePath = Path.GetFullPath(Path.Combine(typesDir, $"./{id}.json"));
      await using var typesFile = File.OpenWrite(typePath);
      await JsonSerializer.SerializeAsync(typesFile, type, IndentedSerializationHelper.Default.TypeSpecification);
    }
  }
}