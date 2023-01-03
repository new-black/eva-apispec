using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;

internal class TypescriptOptions : GenerateOptions
{
  internal string? PackagePrefix { get; set; }
}

internal class TypescriptOptionsBinder : BaseGenerateOptionsBinder<TypescriptOptions>
{
  private static readonly Option<string?> PackagePrefix = new(
    name: "--opt-package-prefix",
    description: "The prefix to use for packages. If the prefix is `@company/eva-services-`, package `EVA.Core.Management` will be exported as `@company/eva-services-core-management"
  );

  protected override IEnumerable<Option> GetOptions()
  {
    yield return PackagePrefix;
  }

  protected override void BuildOptions(TypescriptOptions options, BindingContext ctx)
  {
    options.PackagePrefix = PackagePrefix.Value(ctx);
  }
}