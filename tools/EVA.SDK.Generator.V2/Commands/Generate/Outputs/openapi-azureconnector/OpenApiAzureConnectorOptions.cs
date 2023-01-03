using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi_azureconnector;

internal class OpenApiAzureConnectorOptions : GenerateOptions
{
  internal string Host { get; set; } = OpenApiAzureConnectorOptionsBinder.Host.Default;
}

internal class OpenApiAzureConnectorOptionsBinder : BaseGenerateOptionsBinder<OpenApiAzureConnectorOptions>
{
  internal static readonly OptionWithDefault<string> Host = new Option<string>(
    name: "--opt-host",
    description: "The host to use"
  ).WithDefault("");

  protected override IEnumerable<Option> GetOptions()
  {
    yield return Host.Option;
  }

  protected override void BuildOptions(OpenApiAzureConnectorOptions options, BindingContext ctx)
  {
    options.Host = Host.Value(ctx);
  }
}