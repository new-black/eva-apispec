using System.CommandLine;
using System.CommandLine.Binding;
using System.Text;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate;

public class GenerateOptions
{
  internal string? Input { get; init; }
  internal string OutputDirectory { get; set; } = string.Empty;
  internal bool Overwrite { get; init; }

  // Filters
  internal string[]? FilterAssemblies { get; set; }
  internal string[]? FilterServices { get; init; }

  internal List<string>? Transforms { get; set; }

  internal string? OrphanedTypesAssembly { get; init; }

  internal string? MergeSmallAssemblies { get; init; } = BaseGenerateOptionsBinderOptions.MergeSmallAssemblies.Default;

  internal int MergeSmallAssembliesLimit { get; init; } = BaseGenerateOptionsBinderOptions.MergeSmallAssembliesLimit.Default;

  internal string? MergeSmallAssembliesFilter { get; init; } = BaseGenerateOptionsBinderOptions.MergeSmallAssembliesFilter.Default;

  internal bool UseStringIDs { get; init; } = BaseGenerateOptionsBinderOptions.UseStringIDs.Default;

  internal string Api { get; init; } = BaseGenerateOptionsBinderOptions.Api.Default;

  internal void EnsureTransform(string transform)
  {
    Transforms ??= new List<string>();
    if (!Transforms.Contains(transform)) Transforms.Add(transform);
  }
}

internal static class BaseGenerateOptionsBinderOptions
{
  internal static readonly Option<string> OutputDir = new Option<string>(
    name: "--out",
    description: "The directory to write the output to"
  ) { IsRequired = true }.WithAlias("-o");

  internal static readonly Option<bool> Overwrite = new(
    name: "--overwrite",
    description: "Overwrite the entire output directory if it exists"
  );

  internal static readonly Option<string[]> FilterAssemblies = new(
    name: "--assembly",
    description: "Only output services from this assembly. Can be specified multiple time. Supports format Assembly.Source:Assembly.Target to allow for assembly rewriting."
  ) { AllowMultipleArgumentsPerToken = true };

  internal static readonly Option<string> OrphanedTypesAssembly = new(
    name: "--orphaned-types-assembly",
    description: "Merge all types from assemblies without services into the given assembly."
  );

  internal static readonly Option<string[]> FilterServices = new(
    name: "--service",
    description: "Only output this single service. Can be specified multiple time. This is case sensitive."
  );

  internal static readonly OptionWithDefault<string?> MergeSmallAssemblies = new Option<string?>(
    name: "--merge-small-assemblies",
    description: "Merge all assemblies with 10 (default) services or less into the given assembly. This limit is configurable using --merge-small-assemblies-limit."
  ).WithDefault(null);

  internal static readonly OptionWithDefault<int> MergeSmallAssembliesLimit = new Option<int>(
    name: "--merge-small-assemblies-limit",
    description: "The limit of services to merge into a single assembly. Defaults to 10."
  ).WithDefault(10);

  internal static readonly OptionWithDefault<string?> MergeSmallAssembliesFilter = new Option<string?>(
    name: "--merge-small-assemblies-filter",
    description: "Which assemblie are allowed to be merged due to size. Use this to exclude specific assemblies from being merged."
  ).WithDefault(null);

  internal static readonly OptionWithDefault<List<string>> Removes = new Option<List<string>>(
    name: "--remove",
    description: "Shorthand for all remove-* prefixed transforms. Eg: --remove generics inheritance will map to --transform remove-generics remove-inheritance"
  ) { AllowMultipleArgumentsPerToken = true }.WithDefault(new List<string>());

  internal static readonly OptionWithDefault<bool> UseStringIDs = new Option<bool>(
    name: "--use-string-ids",
    description: "Use string IDs instead of longs/strings for all entities. This will become the standard in the future."
  ).WithDefault(false);

  internal static readonly OptionWithDefault<string> Api = new Option<string>(
    name: "--api",
    description: "The API to output"
  ).WithDefault("eva");
}

internal abstract class BaseGenerateOptionsBinder<T> : BinderBase<T> where T : GenerateOptions, new()
{
  private Option<List<string>> _transforms;

  protected override T GetBoundValue(BindingContext bindingContext)
  {
    var removes = BaseGenerateOptionsBinderOptions.Removes.Value(bindingContext);
    var transforms = _transforms.Value(bindingContext);

    var result = new T
    {
      Input = SharedOptions.Input.Value(bindingContext),
      OutputDirectory = BaseGenerateOptionsBinderOptions.OutputDir.Value(bindingContext) ?? string.Empty,
      Overwrite = BaseGenerateOptionsBinderOptions.Overwrite.Value(bindingContext),
      Transforms = (transforms ?? Enumerable.Empty<string>()).Concat(removes.Select(r => $"remove-{r}")).ToList(),
      FilterAssemblies = BaseGenerateOptionsBinderOptions.FilterAssemblies.Value(bindingContext),
      FilterServices = BaseGenerateOptionsBinderOptions.FilterServices.Value(bindingContext),
      OrphanedTypesAssembly = BaseGenerateOptionsBinderOptions.OrphanedTypesAssembly.Value(bindingContext),
      MergeSmallAssemblies = BaseGenerateOptionsBinderOptions.MergeSmallAssemblies.Value(bindingContext),
      MergeSmallAssembliesLimit = BaseGenerateOptionsBinderOptions.MergeSmallAssembliesLimit.Value(bindingContext),
      MergeSmallAssembliesFilter = BaseGenerateOptionsBinderOptions.MergeSmallAssembliesFilter.Value(bindingContext),
      UseStringIDs = BaseGenerateOptionsBinderOptions.UseStringIDs.Value(bindingContext),
      Api = BaseGenerateOptionsBinderOptions.Api.Value(bindingContext)
    };

    BuildOptions(result, bindingContext);
    return result;
  }

  public IEnumerable<Option> GetAllOptions(string[] forcedTransformations)
  {
    yield return SharedOptions.Input;
    yield return BaseGenerateOptionsBinderOptions.OutputDir;
    yield return BaseGenerateOptionsBinderOptions.Overwrite;
    yield return BaseGenerateOptionsBinderOptions.FilterAssemblies;
    yield return BaseGenerateOptionsBinderOptions.FilterServices;
    yield return BaseGenerateOptionsBinderOptions.OrphanedTypesAssembly;
    yield return BaseGenerateOptionsBinderOptions.MergeSmallAssemblies.Option;
    yield return BaseGenerateOptionsBinderOptions.MergeSmallAssembliesLimit.Option;
    yield return BaseGenerateOptionsBinderOptions.MergeSmallAssembliesFilter.Option;
    yield return BaseGenerateOptionsBinderOptions.UseStringIDs.Option;
    yield return BaseGenerateOptionsBinderOptions.Api.Option;

    foreach (var opt in GetOptions()) yield return opt;

    yield return BaseGenerateOptionsBinderOptions.Removes.Option;

    // Build the transform options
    var sb = new StringBuilder();
    sb.AppendLine("Available transformations:");

    foreach (var x in GenerationPipeline.Transforms)
    {
      if (forcedTransformations.Contains(x.Name)) continue;
      sb.AppendLine();
      sb.AppendLine($"{x.Name}: {x.Description}");
    }

    _transforms = new Option<List<string>>("--transforms", sb.ToString()) { AllowMultipleArgumentsPerToken = true };
    yield return _transforms;
  }

  protected abstract IEnumerable<Option> GetOptions();

  protected abstract void BuildOptions(T options, BindingContext ctx);
}