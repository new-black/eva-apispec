using EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Writers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi_azureconnector;

internal class OpenApiAzureConnectorOutput : IOutput<OpenApiAzureConnectorOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedTransformations => _openApiOutput.ForcedTransformations;

  private readonly OpenApiOutput _openApiOutput = new();

  public async Task Write(OutputContext<OpenApiAzureConnectorOptions> ctx)
  {
    var model = _openApiOutput.GetModel(ctx.Input, ctx.Options.Host, new());
    
    AzureConnectorExtender.Extend(model, ctx.Input);

    await using var file = ctx.Writer.WriteStreamAsync("openapi.json");
    await using var textWriter = new StreamWriter(file.Value);

    IOpenApiWriter writer = new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings { Terse = true });

    model.Serialize(writer, OpenApiSpecVersion.OpenApi2_0);
  }
}