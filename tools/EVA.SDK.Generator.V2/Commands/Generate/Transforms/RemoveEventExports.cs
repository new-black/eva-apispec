using System.Collections.Immutable;
using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

public class RemoveEventExports : INamedTransform
{
  public string Name => "event-exports";
  public string Description => "Will remove all type information regarding event exports";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    if (!input.EventTargets.Any()) return ITransform.TransformResult.NoChanges;

    input.EventTargets = ImmutableArray<EventTarget>.Empty;
    return ITransform.TransformResult.Changes;

  }
}