using System.Collections.Immutable;
using EVA.Core.Typings.V2;

namespace EVA.SDK.Generator.V2.Generator.Transforms;

public class RemoveDeprecatedServices : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var countBefore = input.Services.Length;
    input.Services = input.Services.Where(s => s.Deprecated == null).ToImmutableArray();

    return countBefore == input.Services.Length ? ITransform.TransformResult.NoChanges : ITransform.TransformResult.Changes;
  }
}