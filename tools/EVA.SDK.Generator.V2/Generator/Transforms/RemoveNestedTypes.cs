using EVA.Core.Typings.V2;

namespace EVA.SDK.Generator.V2.Generator.Transforms;

public class RemoveNestedTypes : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var changes = ITransform.TransformResult.NoChanges;
    foreach (var (_, type) in input.Types)
    {
      if (type.ParentType == null) continue;
      type.ParentType = null;
      changes = ITransform.TransformResult.Changes;
    }

    return changes;
  }
}