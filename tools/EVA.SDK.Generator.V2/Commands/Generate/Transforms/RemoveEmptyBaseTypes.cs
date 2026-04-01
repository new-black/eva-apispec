using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class RemoveEmptyBaseTypes : INamedTransform
{
  public string ID => "remove-empty-base-types";
  public string Description => "Will remove extends relationships where the entire base type chain has no properties.";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var changes = ITransform.TransformResult.None;

    foreach (var (_, type) in input.Types)
    {
      if (type.Extends == null) continue;
      if (!ChainHasNoProperties(type.Extends, input)) continue;

      type.Extends = null;
      changes = ITransform.TransformResult.Changes;
    }

    return changes;
  }

  private static bool ChainHasNoProperties(TypeReference? typeRef, ApiDefinitionModel input)
  {
    if (typeRef == null) return true;
    if (!input.Types.TryGetValue(typeRef.Name, out var type)) return true;
    if (type.Properties.Any()) return false;
    return ChainHasNoProperties(type.Extends, input);
  }
}
