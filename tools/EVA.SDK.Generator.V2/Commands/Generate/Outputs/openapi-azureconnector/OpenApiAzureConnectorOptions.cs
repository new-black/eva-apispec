using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi.Extensions;

public class OpenApiAzureConnectorOptions : GenerateOptions
{
  public string Host { get; set; }
}

public class OpenApiAzureConnectorOptionsBinder : BaseGenerateOptionsBinder<OpenApiAzureConnectorOptions>
{
  private Option<string> Host = new Option<string>(
    name: "--opt-host",
    description: "The host to use"
  ).WithDefault("");

  protected override IEnumerable<Option> GetOptions()
  {
    yield return Host;
  }

  protected override void BuildOptions(OpenApiAzureConnectorOptions options, BindingContext ctx)
  {
    options.Host = Host.Value(ctx);
  }
}