using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.openapi;

public class OpenApiOptions
{
  public string Version { get; set; }
  public bool Terse { get; set; }
  public string Format { get; set; }
  public string Host { get; set; }
  public string Preset { get; set; }
}

public class OpenApiOptionsBinder : BinderBase<OpenApiOptions>, IOptionProvider
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

  private Option<string> Preset = new Option<string>(
    name: "--opt-preset",
    description: "The preset to use. This might override other options."
  ).FromAmong("none", "azure-connector").WithDefault("none");

  protected override OpenApiOptions GetBoundValue(BindingContext ctx)
  {
    return new OpenApiOptions
    {
      Version = Version.Value(ctx),
      Terse = Terse.Value(ctx),
      Format = Format.Value(ctx),
      Host = Host.Value(ctx),
      Preset = Preset.Value(ctx)
    };
  }

  public IEnumerable<Option> GetAllOptions()
  {
    yield return Version;
    yield return Terse;
    yield return Format;
    yield return Host;
    yield return Preset;
  }
}