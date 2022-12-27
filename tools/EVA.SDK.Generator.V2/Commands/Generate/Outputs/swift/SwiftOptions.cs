using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;

public class SwiftOptions : GenerateOptions
{
  public string AnyCodeableName { get; set; }
}

public class SwiftOptionsBinder : BaseGenerateOptionsBinder<SwiftOptions>
{
  private Option<string> AnyCodeableName = new Option<string>(
    name: "--opt-anycodable-name",
    description: "The name to use for AnyCodable"
  ).WithDefault("EvaAnyCodable");

  protected override IEnumerable<Option> GetOptions()
  {
    yield return AnyCodeableName;
  }

  protected override void BuildOptions(SwiftOptions options, BindingContext ctx)
  {
    options.AnyCodeableName = AnyCodeableName.Value(ctx);
  }
}