using System.Collections.Immutable;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Transforms;

public class RemoveErrors : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    if (input.Errors.Any())
    {
      input.Errors = ImmutableArray<ErrorSpecification>.Empty;
      return ITransform.TransformResult.Changes;
    }

    return ITransform.TransformResult.NoChanges;
  }
}