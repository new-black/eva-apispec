using System.Collections.Immutable;
using System.IO.Enumeration;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms.Filters;

internal class FilterAssemblies : ITransform
{
  public string Description => "Filters assemblies";

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
  /// <param name="oldAssemblyName"></param>
  /// <returns></returns>
  string? DetermineTargetAssemblyFromFilters(string oldAssemblyName)
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

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    // Filter the services
    var count1 = input.Services.Length;
    var result = new List<ServiceModel>();
    foreach (var service in input.Services)
    {
      var targetName = DetermineTargetAssemblyFromFilters(service.Assembly);
      if (targetName != null)
      {
        service.Assembly = targetName;
        result.Add(service);
      }
    }

    input.Services = result.ToImmutableArray();

    // Filter the types (renaming actually)
    foreach (var type in input.Types.Values)
    {
      var targetName = DetermineTargetAssemblyFromFilters(type.Assembly);
      if (targetName != null)
      {
        type.Assembly = targetName;
      }
    }

    // We can remove types for assemblies from option types, because they are not required there
    foreach (var tr in input.EnumerateAllTypeReferences())
    {
      if (tr.Name == ApiSpecConsts.Specials.Option)
      {
        tr.Arguments = [..tr.Arguments.Where(x => input.Types.TryGetValue(x.Name, out var type) && DetermineTargetAssemblyFromFilters(type.Assembly) != null)];
      }
    }

    // Filter the errors
    var count2 = input.Errors.Length;
    var result2 = new List<ErrorSpecification>();
    foreach (var error in input.Errors)
    {
      var targetName = DetermineTargetAssemblyFromFilters(error.Assembly);
      if (targetName == null) continue;

      error.Assembly = targetName;
      result2.Add(error);
    }

    input.Errors = result2.ToImmutableArray();

    // Apply grouping of small assemblies
    if (_targetAssembly != null)
    {
      var mergedAssemblies = new HashSet<string>();

      foreach (var assemblyServices in input.Services.GroupBy(s => s.Assembly))
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

      foreach (var service in input.Services)
      {
        if (mergedAssemblies.Contains(service.Assembly)) service.Assembly = _targetAssembly;
      }

      foreach (var type in input.Types.Values)
      {
        if (mergedAssemblies.Contains(type.Assembly)) type.Assembly = _targetAssembly;
      }

      foreach (var error in input.Errors)
      {
        if (mergedAssemblies.Contains(error.Assembly)) error.Assembly = _targetAssembly;
      }
    }

    // Log output
    LogOutput(logger);
    return input.Services.Length == count1 && input.Errors.Length == count2 ? ITransform.TransformResult.None : ITransform.TransformResult.Changes;
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