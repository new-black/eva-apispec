using System.Collections.Immutable;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Transforms;

public class RemoveEventExports : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    if (input.EventTargets.Any())
    {
      input.EventTargets = ImmutableArray<EventTarget>.Empty;
      return ITransform.TransformResult.Changes;
    }

    return ITransform.TransformResult.NoChanges;
  }
}