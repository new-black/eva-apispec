using System.Collections.Immutable;
using System.IO.Enumeration;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms.Filters;

internal class FilterAssemblies : ITransform
{
  private readonly List<(string filter, string? output)> _assemblies;

  public FilterAssemblies(string[] assemblies)
  {
    _assemblies = assemblies.Select(x => x.Split(':')).Select(x => (x[0],x.Skip(1).FirstOrDefault())).ToList();
  }

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var cache = new Dictionary<string, string?>();

    string? Transform(string input)
    {
      if (cache.TryGetValue(input, out var result)) return result;

      foreach (var (filter,output) in _assemblies)
      {
        if (!FileSystemName.MatchesSimpleExpression(filter, input)) continue;

        result = output ?? input;
        break;
      }

      cache[input] = result;
      return result;
    }

    // Validate the filters
    var loggableAssemblies = input.EnumerateAllAssemblies().Select(assembly => (assembly, mappedTo: Transform(assembly))).ToList();

    Console.WriteLine("\nKeeping assemblies:");
    foreach (var x in loggableAssemblies.Where(x => x.mappedTo == x.assembly))
    {
      Console.WriteLine($"- {x.assembly}");
    }

    Console.WriteLine("\nGrouping:");
    foreach (var x in loggableAssemblies.Where(x => x.mappedTo != null && x.mappedTo != x.assembly).GroupBy(x => x.mappedTo))
    {
      Console.WriteLine($"  {x.Key}");
      foreach (var y in x)
      {
        Console.WriteLine($"  - {y.assembly}");
      }
    }

    Console.WriteLine("\nRemoving:");
    foreach (var x in loggableAssemblies.Where(x => x.mappedTo == null))
    {
      Console.WriteLine($"- {x.assembly}");
    }

    // Filter the services
    var count1 = input.Services.Length;
    var result = new List<ServiceModel>();
    foreach (var service in input.Services)
    {
      var targetName = Transform(service.Assembly);
      if (targetName != null)
      {
        service.Assembly = targetName;
        result.Add(service);
      }
    }
    input.Services = result.ToImmutableArray();

    // Filter the types
    foreach (var type in input.Types.Values)
    {
      var targetName = Transform(type.Assembly);
      if (targetName != null)
      {
        type.Assembly = targetName;
      }
    }

    // Filter the errors
    var count2 = input.Errors.Length;
    var result2 = new List<ErrorSpecification>();
    foreach (var error in input.Errors)
    {
      var targetName = Transform(error.Assembly);
      if (targetName != null)
      {
        error.Assembly = targetName;
        result2.Add(error);
      }
    }
    input.Errors = result2.ToImmutableArray();

    // Orphaned types
    if (options.OrphanedTypesAssembly != null)
    {
      var serviceAssemblies = input.Services.Select(s => s.Assembly).ToHashSet();
      foreach (var type in input.Types.Values)
      {
        if (!serviceAssemblies.Contains(type.Assembly))
        {
          type.Assembly = options.OrphanedTypesAssembly;
        }
      }
    }

    return input.Services.Length == count1 && input.Errors.Length == count2 ? ITransform.TransformResult.None : ITransform.TransformResult.Changes;
  }
}