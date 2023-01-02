using System.CommandLine;
using System.CommandLine.Binding;
using System.Text;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate;

public class GenerateOptions
{
  public string? Input { get; set; }
  public string OutputDirectory { get; set; } = string.Empty;



  public bool Overwrite { get; set; }

  // Filters
  public string[]? FilterAssemblies { get; set; }
  public string[]? FilterServices { get; set; }

  public List<string>? Remove { get; set; }

  public string? OrphanedTypesAssembly { get; set; }

  public void EnsureRemove(string remove)
  {
    Remove ??= new List<string>();
    if (!Remove.Contains(remove)) Remove.Add(remove);
  }
}

public abstract class BaseGenerateOptionsBinder<T> : BinderBase<T> where T : GenerateOptions, new()
{
  public readonly Option<string> OutputDir = new Option<string>(
    name: "--out",
    description: "The directory to write the output to"
  ) { IsRequired = true }.WithAlias("-o");

  public readonly Option<bool> Overwrite = new(
    name: "--overwrite",
    description: "Overwrite the entire output directory if it exists"
  );

  public readonly Option<string[]> FilterAssemblies = new(
    name: "--assembly",
    description: "Only output services from this assembly. Can be specified multiple time. Supports format Assembly.Source:Assembly.Target to allow for assembly rewriting."
  ) { AllowMultipleArgumentsPerToken = true };

  public readonly Option<string> OrphanedTypesAssembly = new(
    name: "--orphaned-types-assembly",
    description: "Merge all types from assemblies without services into the given assembly."
  );

  public readonly Option<string[]> FilterServices = new(
    name: "--service",
    description: "Only output this single service. Can be specified multiple time. This is case sensitive."
  );

  protected override T GetBoundValue(BindingContext ctx)
  {
    var result = new T
    {
      Input = SharedOptions.Input.Value(ctx),
      OutputDirectory = OutputDir.Value(ctx) ?? string.Empty,
      Overwrite = Overwrite.Value(ctx),
      Remove = _remove?.Value(ctx),
      FilterAssemblies = FilterAssemblies.Value(ctx),
      FilterServices = FilterServices.Value(ctx),
      OrphanedTypesAssembly = OrphanedTypesAssembly.Value(ctx)
    };

    BuildOptions(result, ctx);
    return result;
  }

  private Option<List<string>>? _remove;

  public IEnumerable<Option> GetAllOptions(string[] forcedRemoves)
  {
    yield return SharedOptions.Input;
    yield return OutputDir;
    yield return Overwrite;
    yield return FilterAssemblies;
    yield return FilterServices;
    yield return OrphanedTypesAssembly;

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

  protected abstract void BuildOptions(T options, BindingContext bindingContext);
}