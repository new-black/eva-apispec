using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class RemoveNestedTypes : INamedTransform
{
  public string ID => "remove-nested-types";
  public string Description => "Will remove nested types by bringing them to the root level";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var changes = ITransform.TransformResult.None;
    foreach (var (_, type) in input.Types)
    {
      if (type.ParentType == null) continue;
      type.ParentType = null;
      changes = ITransform.TransformResult.Changes;
    }

    return changes;
  }
}