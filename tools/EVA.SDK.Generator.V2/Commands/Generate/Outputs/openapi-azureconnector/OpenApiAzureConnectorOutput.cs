using EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Writers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi_azureconnector;

internal class OpenApiAzureConnectorOutput : IOutput<OpenApiAzureConnectorOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedRemoves => new[] { "generics", "unused-type-params", "errors", "inheritance", "datalake-exports" };

  public async Task Write(OutputContext<OpenApiAzureConnectorOptions> ctx)
  {
    var model = OpenApiOutput.GetModel(ctx.Input, ctx.Options.Host);
    AzureConnectorExtender.Extend(model, ctx.Input);

    await using var file = ctx.Writer.WriteStreamAsync("openapi.json");
    await using var textWriter = new StreamWriter(file.Value);

    IOpenApiWriter writer = new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings { Terse = true });

    model.Serialize(writer, OpenApiSpecVersion.OpenApi2_0);
  }
}