using EVA.API.Spec;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Writers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi.Extensions;

public class OpenApiAzureConnectorOutput : IOutput<OpenApiAzureConnectorOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedRemoves => new[] { "generics", "unused-type-params", "errors", "inheritance" };

  public async Task Write(ApiDefinitionModel input, OpenApiAzureConnectorOptions options)
  {
    var outputPath = Path.GetFullPath(Path.Combine(options.OutputDirectory, "openapi.json"));
    Console.WriteLine($"Writing OpenAPI file: {outputPath}");

    var model = OpenApiOutput.GetModel(input, options.Host);
    AzureConnectorExtender.Extend(model, input);

    await using var file = File.OpenWrite(outputPath);
    await using var textWriter = new StreamWriter(file);
    IOpenApiWriter writer = new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings { Terse = true });

    model.Serialize(writer, OpenApiSpecVersion.OpenApi2_0);
  }
}