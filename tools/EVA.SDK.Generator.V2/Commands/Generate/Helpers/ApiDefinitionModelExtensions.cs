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
}