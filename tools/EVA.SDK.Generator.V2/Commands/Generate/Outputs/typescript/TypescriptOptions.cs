using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;

internal class TypescriptOptions : GenerateOptions
{
  internal string? PackagePrefix { get; set; } = TypescriptOptionsBinder.PackagePrefix.Default;
  internal bool Extenders { get; set; } = TypescriptOptionsBinder.Extenders.Default;
}

internal class TypescriptOptionsBinder : BaseGenerateOptionsBinder<TypescriptOptions>
{
  internal static readonly OptionWithDefault<string?> PackagePrefix = new Option<string?>(
    name: "--opt-package-prefix",
    description: "The prefix to use for packages. If the prefix is `company/`, package `EVA.Core.Management` will be exported as `company/eva-services-core-management. If you want this to start with a `@`, use '\\\\', eg: '\\\\company/'."
  ).WithDefault(null);

  internal static readonly OptionWithDefault<bool> Extenders = new Option<bool>(
    name: "--opt-extenders",
    description: "Add extenders"
  ).WithDefault(false);

  protected override IEnumerable<Option> GetOptions()
  {
    yield return PackagePrefix.Option;
    yield return Extenders.Option;
  }

  protected override void BuildOptions(TypescriptOptions options, BindingContext ctx)
  {
    var prefix = PackagePrefix.Value(ctx);
    if (prefix.StartsWith(@"\\")) prefix = $"@{prefix[2..]}";
    options.PackagePrefix = prefix;

    options.Extenders = Extenders.Value(ctx);
  }
}