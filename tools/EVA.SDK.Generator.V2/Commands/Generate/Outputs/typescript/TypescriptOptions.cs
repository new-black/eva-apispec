using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;

public class TypescriptOptions
{
  public string? PackagePrefix { get; set; }
}

public class TypescriptOptionsBinder : BinderBase<TypescriptOptions>, IOptionProvider
{
  private Option<string?> PackagePrefix = new Option<string?>(
    name: "--opt-package-prefix",
    description: "The prefix to use for packages. If the prefix is `@company/eva-services-`, package `EVA.Core.Management` will be exported as `@company/eva-services-core-management"
  ).WithDefault(null);

  protected override TypescriptOptions GetBoundValue(BindingContext ctx)
  {
    return new TypescriptOptions
    {
      PackagePrefix = PackagePrefix.Value(ctx)
    };
  }

  public IEnumerable<Option> GetAllOptions()
  {
    yield return PackagePrefix;
  }
}