using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;

internal class SwiftOptions : GenerateOptions
{
  internal string AnyCodableName { get; set; } = SwiftOptionsBinder.AnyCodableName.Default;
  internal bool OptimisticNullability { get; set; } = SwiftOptionsBinder.OptimisticNullability.Default;
}

internal class SwiftOptionsBinder : BaseGenerateOptionsBinder<SwiftOptions>
{
  internal static readonly OptionWithDefault<string> AnyCodableName = new Option<string>(
    name: "--opt-anycodable-name",
    description: "The name to use for AnyCodable"
  ).WithDefault("EvaAnyCodable");

  internal static readonly OptionWithDefault<bool> OptimisticNullability = new Option<bool>(
    name: "--opt-optimistic-nullability",
    description: "Handle nullability optimistically. This will PRETEND that generic arguments or array elements are never null."
  ).WithDefault(false);

  protected override IEnumerable<Option> GetOptions()
  {
    yield return AnyCodableName.Option;
    yield return OptimisticNullability.Option;
  }

  protected override void BuildOptions(SwiftOptions options, BindingContext ctx)
  {
    options.AnyCodableName = AnyCodableName.Value(ctx);
    options.OptimisticNullability = OptimisticNullability.Value(ctx);
  }
}