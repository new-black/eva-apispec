using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Transforms;

public class RemoveOptions : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var changes = ITransform.TransformResult.NoChanges;
    foreach (var typeReference in input.EnumerateAllTypeReferences())
    {
      if (typeReference.Name == "option")
      {
        // If the shared object does not have any objects, and does not extend any other object. Flatten this to an generic object
        var sharedSpec = input.Types[typeReference.Shared.Name];
        if ((sharedSpec.Properties == null || !sharedSpec.Properties.Any()) && sharedSpec.Extends == null)
        {
          typeReference.Name = "object";
          typeReference.Arguments = ImmutableArray<TypeReference>.Empty;
          typeReference.Nullable = typeReference.Shared.Nullable;
          typeReference.Shared = null;
        }
        else
        {
          typeReference.Name = typeReference.Shared.Name;
          typeReference.Arguments = typeReference.Shared.Arguments;
          typeReference.Nullable = typeReference.Shared.Nullable;
          typeReference.Shared = null;
        }

        changes = ITransform.TransformResult.Changes;
      }
    }

    return changes;
  }
}