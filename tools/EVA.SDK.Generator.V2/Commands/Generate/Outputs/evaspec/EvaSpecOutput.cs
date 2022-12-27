using System.Text.Json;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.evaspec;

public class EvaSpecOutput : IOutput<EvaSpecOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedRemoves => Array.Empty<string>();

  public async Task Write(ApiDefinitionModel input, EvaSpecOptions options)
  {
    // Main spec file
    var specFile = Path.Combine(options.OutputDirectory, "spec.json");

    Console.WriteLine($"Writing EVA Spec file: {specFile}");
    await using var file = File.OpenWrite(specFile);
    await JsonSerializer.SerializeAsync(file, input, JsonContext.Default.ApiDefinitionModel);

    // Source files
    if (!options.IncludeSrcFiles) return;

    // Setup dirs
    var servicesDir = Path.GetFullPath(Path.Combine(options.OutputDirectory, "./src/services"));
    var typesDir = Path.GetFullPath(Path.Combine(options.OutputDirectory, "./src/types"));
    Directory.CreateDirectory(servicesDir);
    Directory.CreateDirectory(typesDir);

    // Write things
    foreach (var service in input.Services)
    {
      var servicePath = Path.GetFullPath(Path.Combine(servicesDir, $"./{service.Name}.json"));
      await using var serviceFile = File.OpenWrite(servicePath);
      await JsonSerializer.SerializeAsync(serviceFile, service, JsonContext.Indented.ServiceModel);
    }
    foreach (var (id, type) in input.Types)
    {
      var typePath = Path.GetFullPath(Path.Combine(typesDir, $"./{id}.json"));
      await using var typesFile = File.OpenWrite(typePath);
      await JsonSerializer.SerializeAsync(typesFile, type, JsonContext.Indented.TypeSpecification);
    }
  }
}