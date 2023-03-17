using System.CommandLine;
using System.CommandLine.Binding;
using System.Text;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate;

public class GenerateOptions
{
  internal string? Input { get; init; }
  internal string OutputDirectory { get; set; } = string.Empty;
  internal bool Overwrite { get; init; }

  // Filters
  internal string[]? FilterAssemblies { get; init; }
  internal string[]? FilterServices { get; init; }

  internal List<string>? Remove { get; set; }

  internal string? OrphanedTypesAssembly { get; init; }

  internal void EnsureRemove(string remove)
  {
    Remove ??= new List<string>();
    if (!Remove.Contains(remove)) Remove.Add(remove);
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
}

internal abstract class BaseGenerateOptionsBinder<T> : BinderBase<T> where T : GenerateOptions, new()
{
  protected override T GetBoundValue(BindingContext bindingContext)
  {
    var result = new T
    {
      Input = SharedOptions.Input.Value(bindingContext),
      OutputDirectory = BaseGenerateOptionsBinderOptions.OutputDir.Value(bindingContext) ?? string.Empty,
      Overwrite = BaseGenerateOptionsBinderOptions.Overwrite.Value(bindingContext),
      Remove = _remove?.Value(bindingContext),
      FilterAssemblies = BaseGenerateOptionsBinderOptions.FilterAssemblies.Value(bindingContext),
      FilterServices = BaseGenerateOptionsBinderOptions.FilterServices.Value(bindingContext),
      OrphanedTypesAssembly = BaseGenerateOptionsBinderOptions.OrphanedTypesAssembly.Value(bindingContext)
    };

    BuildOptions(result, bindingContext);
    return result;
  }

  private Option<List<string>>? _remove;

  public IEnumerable<Option> GetAllOptions(string[] forcedRemoves)
  {
    yield return SharedOptions.Input;
    yield return BaseGenerateOptionsBinderOptions.OutputDir;
    yield return BaseGenerateOptionsBinderOptions.Overwrite;
    yield return BaseGenerateOptionsBinderOptions.FilterAssemblies;
    yield return BaseGenerateOptionsBinderOptions.FilterServices;
    yield return BaseGenerateOptionsBinderOptions.OrphanedTypesAssembly;

    foreach (var opt in GetOptions()) yield return opt;

    // Build the remove options
    var sb = new StringBuilder();
    sb.AppendLine("Elements to remove:");
    foreach (var x in GenerationPipeline.Transforms)
    {
      if (forcedRemoves.Contains(x.Name)) continue;
      sb.AppendLine();
      sb.AppendLine($"{x.Name}: {x.Description}");
    }

    _remove = new Option<List<string>>("--remove", sb.ToString()) { AllowMultipleArgumentsPerToken = true };
    yield return _remove;
  }

  protected abstract IEnumerable<Option> GetOptions();

  protected abstract void BuildOptions(T options, BindingContext ctx);
}