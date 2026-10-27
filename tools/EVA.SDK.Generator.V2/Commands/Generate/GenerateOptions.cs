using System.CommandLine;
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

  internal List<string> Transforms { get; set; } = [];

  internal string? OrphanedTypesAssembly { get; init; }

  internal string? MergeSmallAssemblies { get; init; } = BaseGenerateOptionsBinderOptions.MergeSmallAssemblies.Default;

  internal int MergeSmallAssembliesLimit { get; init; } = BaseGenerateOptionsBinderOptions.MergeSmallAssembliesLimit.Default;

  internal string? MergeSmallAssembliesFilter { get; init; } = BaseGenerateOptionsBinderOptions.MergeSmallAssembliesFilter.Default;

  internal bool UseStringIDs { get; init; } = BaseGenerateOptionsBinderOptions.UseStringIDs.Default;

  internal string Api { get; init; } = BaseGenerateOptionsBinderOptions.Api.Default;
}

internal static class BaseGenerateOptionsBinderOptions
{
  internal static readonly Option<string> OutputDir = new Option<string>(
    name: "--out"
  )
  {
    Description = "The directory to write the output to",
    Required = true
  }.WithAlias("-o");

  internal static readonly Option<bool> Overwrite = new(
    name: "--overwrite"
  )
  {
    Description = "Overwrite the entire output directory if it exists"
  };

  internal static readonly Option<string[]> FilterAssemblies = new(
    name: "--assembly"
  )
  {
    Description = "Only output services from this assembly. Can be specified multiple time. Supports format Assembly.Source:Assembly.Target to allow for assembly rewriting.",
    AllowMultipleArgumentsPerToken = true
  };

  internal static readonly Option<string> OrphanedTypesAssembly = new(
    name: "--orphaned-types-assembly"
  )
  {
    Description = "Merge all types from assemblies without services into the given assembly."
  };

  internal static readonly Option<string[]> FilterServices = new(
    name: "--service"
  )
  {
    Description = "Only output this single service. Can be specified multiple time. This is case sensitive."
  };

  internal static readonly OptionWithDefault<string?> MergeSmallAssemblies = new Option<string?>(
    name: "--merge-small-assemblies"
  )
  {
    Description = "Merge all assemblies with 10 (default) services or less into the given assembly. This limit is configurable using --merge-small-assemblies-limit."
  }.WithDefault(null);

  internal static readonly OptionWithDefault<int> MergeSmallAssembliesLimit = new Option<int>(
    name: "--merge-small-assemblies-limit"
  )
  {
    Description = "The limit of services to merge into a single assembly. Defaults to 10."
  }.WithDefault(10);

  internal static readonly OptionWithDefault<string?> MergeSmallAssembliesFilter = new Option<string?>(
    name: "--merge-small-assemblies-filter"
  )
  {
    Description = "Which assemblie are allowed to be merged due to size. Use this to exclude specific assemblies from being merged."
  }.WithDefault(null);

  internal static readonly OptionWithDefault<List<string>> Removes = new Option<List<string>>(
    name: "--remove"
  )
  {
    Description = "Shorthand for all remove-* prefixed transforms. Eg: --remove generics inheritance will map to --transform remove-generics remove-inheritance",
    AllowMultipleArgumentsPerToken = true
  }.WithDefault(new List<string>());

  internal static readonly OptionWithDefault<bool> UseStringIDs = new Option<bool>(
    name: "--use-string-ids"
  )
  {
    Description = "Use string IDs instead of longs/strings for all entities. This will become the standard in the future."
  }.WithDefault(false);

  internal static readonly OptionWithDefault<string> Api = new Option<string>(
    name: "--api"
  )
  {
    Description = "The API to output"
  }.WithDefault("eva");
}

internal abstract class BaseGenerateOptionsBinder<T> where T : GenerateOptions, new()
{
  private Option<List<string>> _transforms = null!;

  internal T Bind(ParseResult parseResult)
  {
    var removes = BaseGenerateOptionsBinderOptions.Removes.Value(parseResult);
    var transforms = _transforms.Value(parseResult);

    var result = new T
    {
      Input = SharedOptions.Input.Value(parseResult),
      OutputDirectory = BaseGenerateOptionsBinderOptions.OutputDir.Value(parseResult) ?? string.Empty,
      Overwrite = BaseGenerateOptionsBinderOptions.Overwrite.Value(parseResult),
      Transforms = (transforms ?? Enumerable.Empty<string>()).Concat(removes.Select(r => $"remove-{r}")).ToList(),
      FilterAssemblies = BaseGenerateOptionsBinderOptions.FilterAssemblies.Value(parseResult),
      FilterServices = BaseGenerateOptionsBinderOptions.FilterServices.Value(parseResult),
      OrphanedTypesAssembly = BaseGenerateOptionsBinderOptions.OrphanedTypesAssembly.Value(parseResult),
      MergeSmallAssemblies = BaseGenerateOptionsBinderOptions.MergeSmallAssemblies.Value(parseResult),
      MergeSmallAssembliesLimit = BaseGenerateOptionsBinderOptions.MergeSmallAssembliesLimit.Value(parseResult),
      MergeSmallAssembliesFilter = BaseGenerateOptionsBinderOptions.MergeSmallAssembliesFilter.Value(parseResult),
      UseStringIDs = BaseGenerateOptionsBinderOptions.UseStringIDs.Value(parseResult),
      Api = BaseGenerateOptionsBinderOptions.Api.Value(parseResult)
    };

    BuildOptions(result, parseResult);
    return result;
  }

  public IEnumerable<Option> GetAllOptions(Func<INamedTransform, bool> isTransformForced)
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

    foreach (var transform in GenerationPipeline.AllTransforms)
    {
      if (isTransformForced(transform)) continue;
      sb.AppendLine();
      sb.AppendLine($"{transform.ID}: {transform.Description}");
    }

    _transforms = new Option<List<string>>("--transforms") { AllowMultipleArgumentsPerToken = true };
    _transforms.Description = sb.ToString();
    yield return _transforms;
  }

  protected abstract IEnumerable<Option> GetOptions();

  protected abstract void BuildOptions(T options, ParseResult ctx);
}
