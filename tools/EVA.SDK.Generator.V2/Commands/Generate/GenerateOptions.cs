using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate;

public class GenerateOptions
{
  public string? Input { get; set; }

  public string? OutputDirectory { get; set; }
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

public class GenerateOptionsBinder : BinderBase<GenerateOptions>, IOptionProvider
{
  public readonly Option<string> OutputDir = new(
    name: "--out",
    description: "The directory to write the output to"
  ) { IsRequired = true };

  public readonly Option<bool> Overwrite = new(
    name: "--overwrite",
    description: "Overwrite the entire output directory if it exists"
  );

  public readonly Option<List<string>> Remove = new Option<List<string>>(
    name: "--remove",
    description: @"Elements to remove
inheritance: Will flatten all type hierarchies to the concrete types.
generics: Will flatten all generic types to concrete implementations.
unused-type-params: Will remove type parameters that are never used.
empty-types: Will remove types without any properties.
nested-types: Will bring nested types to to root level.
options: Will remove type information where its a ""anyOf"" construction and instead only output whatever is shared among them.
deprecated-properties: Will remove all deprecated properties immediately and not wait till the deprecation hits.
deprecated-services: Will remove all deprecated services immediately and not wait till the deprecation hits.
errors: Will remove all type information about possible errors.
event-exports: Will remove all type information about event exports.
shared-req-res-types: Will split types that are used both as request and response types into two separate types.
"
  ) { AllowMultipleArgumentsPerToken = true }.FromAmong("inheritance", "generics", "unused-type-params", "empty-types", "nested-types", "options", "deprecated-properties",
    "deprecated-properties", "deprecated-services", "errors", "event-exports", "shared-req-res-types");


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

  public GenerateOptionsBinder()
  {
    OutputDir.AddAlias("-o");
  }

  protected override GenerateOptions GetBoundValue(BindingContext ctx)
  {
    return new GenerateOptions
    {
      Input = SharedOptions.Input.Value(ctx),
      OutputDirectory = OutputDir.Value(ctx),
      Overwrite = Overwrite.Value(ctx),

      Remove = Remove.Value(ctx),

      FilterAssemblies = FilterAssemblies.Value(ctx),
      FilterServices = FilterServices.Value(ctx),
      OrphanedTypesAssembly = OrphanedTypesAssembly.Value(ctx)
    };
  }

  public IEnumerable<Option> GetAllOptions()
  {
    yield return SharedOptions.Input;
    yield return OutputDir;
    yield return Remove;
    yield return Overwrite;
    yield return FilterAssemblies;
    yield return FilterServices;
    yield return OrphanedTypesAssembly;
  }
}