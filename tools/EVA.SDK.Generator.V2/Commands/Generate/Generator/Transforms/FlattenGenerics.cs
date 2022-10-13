using System.Collections.Immutable;
using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Transforms;

public class FlattenGenerics : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var toBeChecked = new Stack<TypeSpecification>(input.Types.Values);
    var result = new Dictionary<string, TypeSpecification>(input.Types);

    while (toBeChecked.TryPop(out var type))
    {
      // Skip generics
      if (type.TypeArguments is { Length: > 0 }) continue;

      foreach (var reference in type.EnumerateAllTypeReferences())
      {
        FlattenReference(reference, result, toBeChecked);
      }
    }

    input.Types = result.Where(t => t.Value.TypeArguments.Length == 0).ToImmutableSortedDictionary();

    return ITransform.TransformResult.NoChanges;
  }

  /// <summary>
  /// Will ensure a flattened version of a given TypeReference exists.
  /// </summary>
  /// <param name="reference">The reference that should be flattened</param>
  /// <param name="availableSpecs"></param>
  /// <param name="toBeChecked"></param>
  /// <param name="id">The id as returned from GetFlattenedName()</param>
  /// <param name="alreadyGenerated">The set of already generated types.</param>
  /// <param name="input">The entire input</param>
  /// <returns></returns>
  private void FlattenReference(TypeReference reference, Dictionary<string, TypeSpecification> availableSpecs, Stack<TypeSpecification> toBeChecked)
  {
    // These don't need flattening
    if (reference.Arguments == null || !reference.Arguments.Any() || reference.Name == "option") return;
    if (!char.IsUpper(reference.Name[0])) return;

    var id = GetFlattenedName(reference, false);

    // Cache check
    if (availableSpecs.ContainsKey(id))
    {
      reference.Name = id;
      reference.Arguments = ImmutableArray<TypeReference>.Empty;
      return;
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