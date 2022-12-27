using System.Collections.Immutable;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

public class RemoveErrors : INamedTransform
{
  public string Name => "errors";
  public string Description => "Will remove all type information regarding errors";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    if (!input.Errors.Any()) return ITransform.TransformResult.NoChanges;

    input.Errors = ImmutableArray<ErrorSpecification>.Empty;
    return ITransform.TransformResult.Changes;
  }
}