using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Generator.Transforms;

public class RemoveOptions : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var changes = ITransform.TransformResult.NoChanges;
    foreach (var typeReference in input.EnumerateAllTypeReferences())
    {
      if (typeReference.Name == "option")
      {
        typeReference.Name = typeReference.Shared.Name;
        typeReference.Arguments = typeReference.Shared.Arguments;
        typeReference.Nullable = typeReference.Shared.Nullable;
        typeReference.Shared = null;

        changes = ITransform.TransformResult.Changes;
      }
    }

    return changes;
  }
}