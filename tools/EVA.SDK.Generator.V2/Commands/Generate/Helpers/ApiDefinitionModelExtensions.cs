using System.Collections.Immutable;
using System.Text.Json;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Helpers;

/// <summary>
/// Some helpers that should make it easier to build an output.
/// </summary>
public static class ApiDefinitionModelExtensions
{
  /// <summary>
  /// Enumerates through all TypeReferences that are in the model.
  /// </summary>
  /// <param name="model"></param>
  /// <returns></returns>
  public static IEnumerable<TypeReference> EnumerateAllTypeReferences(this ApiDefinitionModel model)
  {
    foreach (var (_, type) in model.Types)
    {
      foreach (var x in type.EnumerateAllTypeReferences()) yield return x;
    }
  }

  /// <summary>
  /// Lists all assemblies that are available.
  /// </summary>
  /// <param name="model"></param>
  /// <returns></returns>
  public static IEnumerable<string> EnumerateAllAssemblies(this ApiDefinitionModel model)
  {
    var serviceAssemblies = model.Services.Select(s => s.Assembly);
    var typeAssemblies = model.Types.Values.Select(t => t.Assembly);
    return serviceAssemblies.Concat(typeAssemblies).Distinct().OrderBy(x => x).ToList();
  }

  public static IEnumerable<TypeReference> EnumerateAllTypeReferences(this TypeSpecification type)
  {
    if (type.Extends != null)
    {
      foreach (var x in type.Extends.EnumerateAllTypeReferences()) yield return x;
    }

    if (type.Properties != null)
    {
      foreach (var (_, prop) in type.Properties)
      {
        foreach (var x in prop.Type.EnumerateAllTypeReferences()) yield return x;
      }
    }
  }

  public static IEnumerable<TypeReference> EnumerateAllTypeReferences(this TypeReference reference)
  {
    yield return reference;

    if (reference.Shared != null)
    {
      foreach (var x in reference.Shared.EnumerateAllTypeReferences()) yield return x;
    }

    if (reference.Arguments != null)
    {
      foreach (var arg in reference.Arguments)
      {
        foreach (var x in arg.EnumerateAllTypeReferences()) yield return x;
      }
    }
  }

  /// <summary>
  /// Takes a TypeSpecification, creates a copy and replace the type arguments with the specified TypeReferences. This is really useful for flattening generics.
  /// </summary>
  /// <param name="spec"></param>
  /// <param name="mapping"></param>
  /// <returns></returns>
  public static TypeSpecification TemplateWith(this TypeSpecification spec, IEnumerable<TypeReference> mapping)
  {
    // Create a copy first-thing, this makes sure we won't modify the original.
    spec = JsonSerializer.Deserialize(
      JsonSerializer.Serialize(spec, IndentedSerializationHelper.Default.TypeSpecification),
      IndentedSerializationHelper.Default.TypeSpecification
    );

    var mappedTypeArguments = spec.TypeArguments.Zip(mapping).ToDictionary(x => x.First, x => x.Second);

    // Result won't have any type arguments
    spec.TypeArguments = ImmutableArray<string>.Empty;

    foreach (var r in spec.EnumerateAllTypeReferences())
    {
      if (!r.Name.StartsWith("_")) continue;
      if (mappedTypeArguments.TryGetValue(r.Name, out var replaceReference))
      {
        // We copy the nullable, <T?> should be become <int?>
        r.Nullable = r.Nullable || replaceReference.Nullable;
        r.Name = replaceReference.Name;
        r.Arguments = replaceReference.Arguments;
        r.Shared = replaceReference.Shared;
      }
    }

    return spec;
  }

  public static List<GroupedApiDefinitionModel> GroupByAssembly(this ApiDefinitionModel model)
  {
    var servicesGroupedByAssembly = model.Services.GroupBy(s => s.Assembly);
    var typesGroupedByAssembly = model.Types.ToLookupDictionary(t => t.Value.Assembly);
    var errorsGroupedByAssembly = model.Errors.ToLookupDictionary(e => e.Assembly);
    var handledAssemblies = new HashSet<string>();
    var result = new List<GroupedApiDefinitionModel>();

    foreach (var g in servicesGroupedByAssembly)
    {
      var types = typesGroupedByAssembly.GetValueOrDefault(g.Key) ?? new List<KeyValuePair<string, TypeSpecification>>();
      var errors = errorsGroupedByAssembly.GetValueOrDefault(g.Key) ?? new List<ErrorSpecification>();
      result.Add(new GroupedApiDefinitionModel
      {
        Assembly = g.Key,
        Errors = errors,
        Types = new Dictionary<string, TypeSpecification>(types),
        Services = g.ToList()
      });
      handledAssemblies.Add(g.Key);
    }

    foreach (var (assembly, types) in typesGroupedByAssembly.Where(x => !handledAssemblies.Contains(x.Key)))
    {
      var errors = errorsGroupedByAssembly.GetValueOrDefault(assembly) ?? new List<ErrorSpecification>();
      result.Add(new GroupedApiDefinitionModel
      {
        Assembly = assembly,
        Errors = errors,
        Types = new Dictionary<string, TypeSpecification>(types),
        Services = new List<ServiceModel>()
      });
    }

    return result;
  }

  public static string MessageWithEnhancedArguments(this ErrorSpecification spec)
  {
    var message = spec.Message;
    if (spec.Arguments.Any())
    {
      message = string.Format(message, spec.Arguments.Select((a, i) => $"{{{a.Name ?? i.ToString()}:{a.Type.Name}}}").ToArray());
    }
    return message;
  }

  public static PrefixGroupedErrors GroupByPrefix(this List<ErrorSpecification> errors, int skip = 0)
  {
    var rootErrors = new List<(string Name, ErrorSpecification error)>();
    var subErrors = new SortedDictionary<string, List<ErrorSpecification>>();

    foreach (var e in errors)
    {
      var name = e.Name;
      var idx = name.IndexOf(':', skip);
      if (idx == -1)
      {
        rootErrors.Add((name[skip..], e));
      }
      else
      {
        var category = name[skip..idx];
        if (!subErrors.TryGetValue(category, out var l)) subErrors[category] = l = new List<ErrorSpecification>();
        l.Add(e);
      }
    }

    return new PrefixGroupedErrors
    {
      Errors = rootErrors,
      SubErrors = subErrors.ToImmutableSortedDictionary(x => x.Key, x => GroupByPrefix(x.Value, skip + x.Key.Length + 1))
    };
  }

  public static Dictionary<string, long> ToTotals(this ImmutableSortedDictionary<string, EnumValueSpecification> source)
  {
    var result = new Dictionary<string, long>();

    long GetVal(string s)
    {
      if (result.TryGetValue(s, out var v)) return v;
      var spec = source[s];
      result[s] = v = spec.Value + spec.Extends.Sum(GetVal);
      return v;
    }

    foreach (var x in source.Keys) GetVal(x);
    return result;
  }

  public class PrefixGroupedErrors
  {
    public List<(string Name, ErrorSpecification error)> Errors { get; set; }
    public ImmutableSortedDictionary<string, PrefixGroupedErrors> SubErrors { get; set; }
  }

  public class GroupedApiDefinitionModel
  {
    public string Assembly { get; set; }
    public Dictionary<string,TypeSpecification> Types { get; set; }
    public List<ServiceModel> Services { get; set; }
    public List<ErrorSpecification> Errors { get; set; }
  }
}