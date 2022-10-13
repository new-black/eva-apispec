using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Generator.Outputs.swift;

public class SwiftOptions
{
  public string AnyCodeableName { get; set; }
}

public class SwiftOptionsBinder : BinderBase<SwiftOptions>, IOptionProvider
{
  private Option<string> AnyCodeableName = new Option<string>(
    name: "--opt-anycodable-name",
    description: "The name to use for AnyCodable"
  ).WithDefault("EvaAnyCodable");

  protected override SwiftOptions GetBoundValue(BindingContext ctx)
  {
    return new SwiftOptions
    {
      AnyCodeableName = AnyCodeableName.Value(ctx)
    };
  }

  public IEnumerable<Option> GetAllOptions()
  {
    yield return AnyCodeableName;
  }
}