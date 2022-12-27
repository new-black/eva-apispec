using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;

public class OpenApiOptions : GenerateOptions
{
  public string Version { get; set; }
  public bool Terse { get; set; }
  public string Format { get; set; }
  public string Host { get; set; }
}

public class OpenApiOptionsBinder : BaseGenerateOptionsBinder<OpenApiOptions>
{
  private Option<string> Version = new Option<string>(
    name: "--opt-version",
    description: "The OpenAPI version to output"
  ).FromAmong("v2", "v3").WithDefault("v3");

  private Option<bool> Terse = new Option<bool>(
    name: "--opt-terse",
    description: "Remove as much whitespace from the output as possible. Ignore when format:yaml"
  ).WithDefault(false);

  private Option<string> Format = new Option<string>(
    name: "--opt-format",
    description: "The format to output"
  ).WithDefault("json").FromAmong("json", "yaml");

  private Option<string> Host = new Option<string>(
    name: "--opt-host",
    description: "The host to use"
  ).WithDefault("");

  protected override IEnumerable<Option> GetOptions()
  {
    yield return Version;
    yield return Terse;
    yield return Format;
    yield return Host;
  }

  protected override void BuildOptions(OpenApiOptions options, BindingContext ctx)
  {
    options.Version = Version.Value(ctx);
    options.Terse = Terse.Value(ctx);
    options.Format = Format.Value(ctx);
    options.Host = Host.Value(ctx);
  }
}