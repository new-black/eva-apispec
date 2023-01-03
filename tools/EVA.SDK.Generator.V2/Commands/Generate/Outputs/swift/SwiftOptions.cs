using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;

internal class SwiftOptions : GenerateOptions
{
  internal string AnyCodableName { get; set; } = SwiftOptionsBinder.AnyCodableName.Default;
}

internal class SwiftOptionsBinder : BaseGenerateOptionsBinder<SwiftOptions>
{
  internal static readonly OptionWithDefault<string> AnyCodableName = new Option<string>(
    name: "--opt-anycodable-name",
    description: "The name to use for AnyCodable"
  ).WithDefault("EvaAnyCodable");

  protected override IEnumerable<Option> GetOptions()
  {
    yield return AnyCodableName.Option;
  }

  protected override void BuildOptions(SwiftOptions options, BindingContext ctx)
  {
    options.AnyCodableName = AnyCodableName.Value(ctx);
  }
}