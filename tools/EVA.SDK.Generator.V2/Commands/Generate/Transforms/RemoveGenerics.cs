using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class RemoveGenerics : INamedTransform
{
  public string ID => "remove-generics";
  public string Description => "Removes generic types by flattening them.";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var toBeChecked = new Stack<TypeSpecification>(input.Types.Values);
    var result = new Dictionary<string, TypeSpecification>(input.Types);

    var changes = ITransform.TransformResult.None;
    while (toBeChecked.TryPop(out var type))
    {
      // Skip generics
      if (type.TypeArguments is { Length: > 0 }) continue;

      foreach (var reference in type.EnumerateAllTypeReferences())
      {
        if (FlattenReference(reference, result, toBeChecked, logger))
        {
          changes = ITransform.TransformResult.StructuralChanges;
        }
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
  /// <param name="logger"></param>
  /// <returns></returns>
  private static bool FlattenReference(TypeReference reference, IDictionary<string, TypeSpecification> availableSpecs, Stack<TypeSpecification> toBeChecked, ILogger logger)
  {
    // These don't need flattening
    if (reference.Arguments.Length == 0 || reference.Name == ApiSpecConsts.Specials.Option) return false;
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
    logger.LogDebug("Flattening type: {Type}", id);

    var result = availableSpecs[reference.Name];
    result = result.TemplateWith(reference.Arguments);

    availableSpecs[id] = result;
    toBeChecked.Push(result);

    // Fix the reference
    reference.Name = id;
    reference.Arguments = ImmutableArray<TypeReference>.Empty;
    return true;
  }

  private static string GetFlattenedName(TypeReference reference, bool includeNullable)
  {
    var prefix = includeNullable && !reference.Nullable ? "NotNull" : string.Empty;

    if (!reference.Arguments.Any()) return prefix + reference.Name;

    return $"{prefix}{reference.Name}[{string.Join(',', reference.Arguments.Select(x => GetFlattenedName(x, true)))}]";
  }
}