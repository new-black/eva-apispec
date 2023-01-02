using System.Text.Json;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.evaspec;

public class EvaSpecOutput : IOutput<EvaSpecOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedRemoves => Array.Empty<string>();

  public async Task Write(ApiDefinitionModel input, EvaSpecOptions options, OutputWriter writer)
  {
    // Main spec file
    await using (var file = writer.WriteStreamAsync("spec.json"))
    {
      await JsonSerializer.SerializeAsync(file.Value, input, JsonContext.Default.ApiDefinitionModel);
    }

    // Source files
    if (!options.IncludeSrcFiles) return;

    // Write things
    foreach (var service in input.Services)
    {
      await using (var file = writer.WriteStreamAsync($"src/services/{service.Name}.json"))
      {
        await JsonSerializer.SerializeAsync(file.Value, service, JsonContext.Indented.ServiceModel);
      }
    }
    foreach (var (id, type) in input.Types)
    {
      await using (var file = writer.WriteStreamAsync($"src/types/{id}.json"))
      {
        await JsonSerializer.SerializeAsync(file.Value, type, JsonContext.Indented.TypeSpecification);
      }
    }
  }
}