using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

public class FlattenGenerics : INamedTransform
{
  public string Name => "generics";
  public string Description => "Flattens generic types";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var toBeChecked = new Stack<TypeSpecification>(input.Types.Values);
    var result = new Dictionary<string, TypeSpecification>(input.Types);

    var changes = ITransform.TransformResult.NoChanges;
    while (toBeChecked.TryPop(out var type))
    {
      // Skip generics
      if (type.TypeArguments is { Length: > 0 }) continue;

      foreach (var reference in type.EnumerateAllTypeReferences())
      {
        if (FlattenReference(reference, result, toBeChecked)) changes = ITransform.TransformResult.StructuralChanges;
      }
    }

    input.Types = result.Where(t => t.Value.TypeArguments.Length == 0).ToImmutableSortedDictionary();

    return changes;
  }

  /// <summary>
  /// Will ensure a flattened version of a given TypeReference exists.
  /// </summary>
  /// <param name="reference">The reference that should be flattened</param>
  /// <param name="availableSpecs"></param>
  /// <param name="toBeChecked"></param>
  /// <returns></returns>
  private bool FlattenReference(TypeReference reference, Dictionary<string, TypeSpecification> availableSpecs, Stack<TypeSpecification> toBeChecked)
  {
    // These don't need flattening
    if (!reference.Arguments.Any() || reference.Name == "option") return false;
    if (!char.IsUpper(reference.Name[0])) return false;

    var id = GetFlattenedName(reference, false);

    // Cache check
    if (availableSpecs.ContainsKey(id))
    {
      reference.Name = id;
      reference.Arguments = ImmutableArray<TypeReference>.Empty;
      return true;
    }

    // First deep-clone the template
    Console.WriteLine("Found new reference to be flattened: " + id);

    var result = availableSpecs[reference.Name];
    result = result.TemplateWith(reference.Arguments);

    availableSpecs[id] = result;
    toBeChecked.Push(result);

    // Fix the reference
    reference.Name = id;
    reference.Arguments = ImmutableArray<TypeReference>.Empty;
    return true;
  }

  private string GetFlattenedName(TypeReference reference, bool includeNullable)
  {
    var prefix = includeNullable && !reference.Nullable ? "NotNull" : string.Empty;

    if (reference.Arguments == null || !reference.Arguments.Any())
    {
      return prefix + reference.Name;
    }

    return $"{prefix}{reference.Name}[{string.Join(',', reference.Arguments.Select(x => GetFlattenedName(x, true)))}]";
  }
}