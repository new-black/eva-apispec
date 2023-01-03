using System.Text.Json;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.evaspec;

internal class EvaSpecOutput : IOutput<EvaSpecOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedRemoves => Array.Empty<string>();

  public async Task Write(OutputContext<EvaSpecOptions> ctx)
  {
    // Main spec file
    await using (var file = ctx.Writer.WriteStreamAsync("spec.json"))
    {
      await JsonSerializer.SerializeAsync(file.Value, ctx.Input, JsonContext.Default.ApiDefinitionModel);
    }

    // Source files
    if (!ctx.Options.IncludeSrcFiles) return;

    // Write things
    foreach (var service in ctx.Input.Services)
    {
      await using var file = ctx.Writer.WriteStreamAsync($"src/services/{service.Name}.json");
      await JsonSerializer.SerializeAsync(file.Value, service, JsonContext.Indented.ServiceModel);
    }
    foreach (var (id, type) in ctx.Input.Types)
    {
      await using var file = ctx.Writer.WriteStreamAsync($"src/types/{id}.json");
      await JsonSerializer.SerializeAsync(file.Value, type, JsonContext.Indented.TypeSpecification);
    }
  }
}