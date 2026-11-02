using System.IO.Enumeration;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;

/// <summary>
/// Keeps the services of the given assemblies and renames assemblies, see the --assembly and --merge-small-assemblies options.
/// </summary>
internal class FilterAssemblies : IApiTransform
{
  public string ID => "filter-assemblies";

  private readonly string? _targetAssembly;
  private readonly int _smallAssemblyLimit;
  private readonly List<(string filter, string? output)> _assemblyFilters;
  private readonly Dictionary<string, string?> _mappingCache = new();

  public FilterAssemblies(IEnumerable<string>? assemblies, string? targetAssembly, int smallAssemblyLimit)
  {
    _targetAssembly = targetAssembly;
    _smallAssemblyLimit = smallAssemblyLimit;
    _assemblyFilters = assemblies?.Select(x => x.Split(':')).Select(x => (x[0], x.Skip(1).FirstOrDefault())).ToList() ?? new List<(string, string?)>();
  }

  /// <summary>
  /// Returns `null` if it should be filtered.
  /// </summary>
  private string? DetermineTargetAssemblyFromFilters(string oldAssemblyName)
  {
    if (_mappingCache.TryGetValue(oldAssemblyName, out var result)) return result;

    var hasPositiveFilter = _assemblyFilters.Any(x => !x.filter.StartsWith("-"));

    foreach (var (filter, output) in _assemblyFilters)
    {
      if (filter.StartsWith("-"))
      {
        if (FileSystemName.MatchesSimpleExpression(filter[1..], oldAssemblyName))
        {
          // Should be removed
          _mappingCache[oldAssemblyName] = null;
          return null;
        }
      }
      else
      {
        if (FileSystemName.MatchesSimpleExpression(filter, oldAssemblyName))
        {
          _mappingCache[oldAssemblyName] = output ?? oldAssemblyName;
          return output ?? oldAssemblyName;
        }
      }
    }

    var fallbackResult = hasPositiveFilter ? null : oldAssemblyName;
    _mappingCache[oldAssemblyName] = fallbackResult;
    return fallbackResult;
  }

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var changes = false;

    // Filter the services
    foreach (var service in api.Services.Where(s => DetermineTargetAssemblyFromFilters(s.Assembly) == null).ToList())
    {
      api.RemoveService(service);
      changes = true;
    }

    // Implementations of options from removed assemblies are not required
    foreach (var schema in api.RootSchemas().SelectMany(Schemas.Descendants).OfType<OpenApiSchema>().Where(s => s.Extension(ExtensionNames.Option) != null).ToList())
    {
      schema.AnyOf = schema.AnyOf!
        .Where(o => o is not OpenApiSchemaReference r || DetermineTargetAssemblyFromFilters(api.TypeOfComponent(r.ComponentName()).Assembly) != null)
        .ToList();
    }

    // Rename the types, a service belongs to the assembly of its request type so it is renamed with it
    foreach (var type in api.AllTypes)
    {
      if (DetermineTargetAssemblyFromFilters(type.Assembly) is { } targetName) type.Assembly = targetName;
    }

    // Filter the errors
    foreach (var error in api.Errors.ToList())
    {
      if (DetermineTargetAssemblyFromFilters(error.Assembly) is { } targetName)
      {
        error.Assembly = targetName;
      }
      else
      {
        api.Errors.Remove(error);
        changes = true;
      }
    }

    api.Invalidate();

    // Apply grouping of small assemblies
    if (_targetAssembly != null)
    {
      var mergedAssemblies = new HashSet<string>();

      foreach (var assemblyServices in api.Services.GroupBy(s => s.Assembly))
      {
        if (options.MergeSmallAssembliesFilter is { } filter)
        {
          if (filter.StartsWith("-"))
          {
            if (FileSystemName.MatchesSimpleExpression(filter[1..], assemblyServices.Key))
            {
              continue;
            }
          }
          else
          {
            if (!FileSystemName.MatchesSimpleExpression(filter, assemblyServices.Key))
            {
              continue;
            }
          }
        }

        // We'll never merge the EVA.DataLake assembly
        if (assemblyServices.Key != "EVA.DataLake" && assemblyServices.Count() <= _smallAssemblyLimit)
        {
          mergedAssemblies.Add(assemblyServices.Key);
          // Only for display reasons
          _mappingCache[assemblyServices.Key] = _targetAssembly;
        }
      }

      foreach (var type in api.AllTypes)
      {
        if (mergedAssemblies.Contains(type.Assembly)) type.Assembly = _targetAssembly;
      }

      foreach (var error in api.Errors)
      {
        if (mergedAssemblies.Contains(error.Assembly)) error.Assembly = _targetAssembly;
      }

      api.Invalidate();
    }

    LogOutput(logger);
    return changes;
  }

  private void LogOutput(ILogger logger)
  {
    var ordered = _mappingCache.OrderBy(x => x.Key).ToList();

    foreach (var x in ordered.Where(x => x.Key == x.Value))
    {
      logger.LogInformation("Keeping assembly: {AssemblyName}", x.Key);
    }

    foreach (var x in ordered.Where(x => x.Value != null && x.Key != x.Value).GroupBy(x => x.Value))
    {
      foreach (var y in x)
      {
        logger.LogInformation("Merging {SourceAssemblyName} into {TargetAssemblyName}", y.Key, x.Key);
      }
    }

    foreach (var x in ordered.Where(x => x.Value == null))
    {
      logger.LogInformation("Removing assembly: {AssemblyName}", x.Key);
    }
  }
}

/// <summary>
/// Keeps only the given services, see the --service option.
/// </summary>
internal class FilterServices(IEnumerable<string> services) : IApiTransform
{
  private readonly HashSet<string> _services = services.ToHashSet();

  public string ID => "filter-services";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var removed = api.Services.Where(s => !_services.Contains(s.Name)).ToList();
    foreach (var service in removed) api.RemoveService(service);
    return removed.Count > 0;
  }
}
