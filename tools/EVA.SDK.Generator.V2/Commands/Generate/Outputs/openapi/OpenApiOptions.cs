using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;

internal class OpenApiOptions : GenerateOptions
{
  internal string Version { get; set; } = OpenApiOptionsBinder.Version.Default;
  internal bool Terse { get; set; } = OpenApiOptionsBinder.Terse.Default;
  internal string Format { get; set; } = OpenApiOptionsBinder.Format.Default;
  internal string Host { get; set; } = OpenApiOptionsBinder.Host.Default;
  internal string? ExamplesFolder { get; set; } = OpenApiOptionsBinder.ExamplesFolder.Default;
}

internal class OpenApiOptionsBinder : BaseGenerateOptionsBinder<OpenApiOptions>
{
  internal static readonly OptionWithDefault<string> Version = new Option<string>(
    name: "--opt-version",
    description: "The OpenAPI version to output"
  ).FromAmong("v2", "v3").WithDefault("v3");

  internal static readonly OptionWithDefault<bool> Terse = new Option<bool>(
    name: "--opt-terse",
    description: "Remove as much whitespace from the output as possible. Ignore when format:yaml"
  ).WithDefault(false);

  internal static readonly OptionWithDefault<string> Format = new Option<string>(
    name: "--opt-format",
    description: "The format to output"
  ).FromAmong("json", "yaml").WithDefault("json");

  internal static readonly OptionWithDefault<string> Host = new Option<string>(
    name: "--opt-host",
    description: "The host to use"
  ).WithDefault("");

  internal static readonly OptionWithDefault<string?> ExamplesFolder = new Option<string?>(
    name: "--opt-examples-folder",
    description: "The folder to use for examples"
  ).WithDefault(null);

  protected override IEnumerable<Option> GetOptions()
  {
    yield return Version.Option;
    yield return Terse.Option;
    yield return Format.Option;
    yield return Host.Option;
    yield return ExamplesFolder.Option;
  }

  protected override void BuildOptions(OpenApiOptions options, BindingContext ctx)
  {
    options.Version = Version.Value(ctx);
    options.Terse = Terse.Value(ctx);
    options.Format = Format.Value(ctx);
    options.Host = Host.Value(ctx);
    options.ExamplesFolder = ExamplesFolder.Value(ctx);
  }
}