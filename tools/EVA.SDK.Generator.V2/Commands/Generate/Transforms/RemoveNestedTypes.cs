using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

public class RemoveNestedTypes : INamedTransform
{
  public string Name => "nested-types";
  public string Description => "Will bring nested types to to root level";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
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