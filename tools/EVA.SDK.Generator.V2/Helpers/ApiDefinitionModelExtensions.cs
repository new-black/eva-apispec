using System.Collections.Immutable;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Helpers;

/// <summary>
/// Some helpers that should make it easier to build an output.
/// </summary>
internal static class ApiDefinitionModelExtensions
{
  /// <summary>
  /// Enumerates through all TypeReferences that are in the model.
  /// </summary>
  /// <param name="model"></param>
  /// <returns></returns>
  internal static IEnumerable<TypeReference> EnumerateAllTypeReferences(this ApiDefinitionModel model)
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
  internal static IEnumerable<string> EnumerateAllAssemblies(this ApiDefinitionModel model)
  {
    var serviceAssemblies = model.Services.Select(s => s.Assembly);
    var typeAssemblies = model.Types.Values.Select(t => t.Assembly);
    return serviceAssemblies.Concat(typeAssemblies).Distinct().OrderBy(x => x).ToList();
  }

  internal static IEnumerable<TypeReference> EnumerateAllTypeReferences(this TypeSpecification type)
  {
    if (type.Extends != null)
    {
      foreach (var x in type.Extends.EnumerateAllTypeReferences()) yield return x;
    }

    foreach (var (_, prop) in type.Properties)
    {
      foreach (var x in prop.Type.EnumerateAllTypeReferences()) yield return x;
    }
  }

  internal static IEnumerable<TypeReference> EnumerateAllTypeReferences(this TypeReference reference)
  {
    yield return reference;

    if (reference.Shared != null)
    {
      foreach (var x in reference.Shared.EnumerateAllTypeReferences()) yield return x;
    }

    foreach (var x in reference.Arguments.SelectMany(arg => arg.EnumerateAllTypeReferences()))
    {
      yield return x;
    }
  }

  /// <summary>
  /// Takes a TypeSpecification, creates a copy and replace the type arguments with the specified TypeReferences. This is really useful for flattening generics.
  /// </summary>
  /// <param name="spec"></param>
  /// <param name="mapping"></param>
  /// <returns></returns>
  internal static TypeSpecification TemplateWith(this TypeSpecification spec, IEnumerable<TypeReference> mapping)
  {
    // Create a copy first-thing, this makes sure we won't modify the original.
    spec = JsonContext.Default.TypeSpecification.Clone(spec);

    var mappedTypeArguments = spec.TypeArguments.Zip(mapping).ToDictionary(x => x.First, x => x.Second);

    // Result won't have any type arguments
    spec.TypeArguments = ImmutableArray<string>.Empty;

    foreach (var r in spec.EnumerateAllTypeReferences())
    {
      if (!r.Name.StartsWith("_")) continue;
      if (!mappedTypeArguments.TryGetValue(r.Name, out var replaceReference)) continue;

      // We copy the nullable, <T?> should be become <int?>
      r.Nullable = r.Nullable || replaceReference.Nullable;
      r.Name = replaceReference.Name;
      r.Arguments = replaceReference.Arguments;
      r.Shared = replaceReference.Shared;
    }

    return spec;
  }

  internal static List<GroupedApiDefinitionModel> GroupByAssembly(this ApiDefinitionModel model)
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
      result.Add(new GroupedApiDefinitionModel(g.Key, new Dictionary<string, TypeSpecification>(types), g.ToList(), errors));
      handledAssemblies.Add(g.Key);
    }

    foreach (var (assembly, types) in typesGroupedByAssembly.Where(x => !handledAssemblies.Contains(x.Key)))
    {
      var errors = errorsGroupedByAssembly.GetValueOrDefault(assembly) ?? new List<ErrorSpecification>();
      result.Add(new GroupedApiDefinitionModel(assembly, new Dictionary<string, TypeSpecification>(types), new List<ServiceModel>(), errors));
    }

    return result;
  }

  internal static string MessageWithEnhancedArguments(this ErrorSpecification spec)
  {
    var message = spec.Message;
    if (spec.Arguments.Any())
    {
      message = string.Format(message, spec.Arguments.Select(a => (object)$"{{{a.Name}:{a.Type.Name}}}").ToArray());
    }

    return message;
  }

  internal static PrefixGroupedErrors GroupByPrefix(this List<ErrorSpecification> errors, int skip = 0)
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

    return new PrefixGroupedErrors(
      rootErrors,
      subErrors.ToImmutableSortedDictionary(x => x.Key, x => GroupByPrefix(x.Value, skip + x.Key.Length + 1))
    );
  }

  internal static Dictionary<string, long> ToTotals(this ImmutableSortedDictionary<string, EnumValueSpecification> source)
  {
    var result = new Dictionary<string, long>();

    long GetVal(string s)
    {
      if (result.TryGetValue(s, out var v)) return v;
      var spec = source[s];
      result[s] = v = spec.Extends.Aggregate(spec.Value, (a, b) => a | GetVal(b));
      return v;
    }

    foreach (var x in source.Keys) GetVal(x);
    return result;
  }

  internal static TypeReference CloneAsNotNull(this TypeReference reference)
  {
    return !reference.Nullable
      ? reference
      : new TypeReference(reference.Name, reference.Arguments, false) { Shared = reference.Shared };
  }

  internal record PrefixGroupedErrors(List<(string Name, ErrorSpecification error)> Errors, ImmutableSortedDictionary<string, PrefixGroupedErrors> SubErrors);

  internal record GroupedApiDefinitionModel(string Assembly, Dictionary<string, TypeSpecification> Types, List<ServiceModel> Services, List<ErrorSpecification> Errors);
}