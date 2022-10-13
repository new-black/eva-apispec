using System.Collections.Immutable;
using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Generator.Transforms;

public class FixDependencies : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    foreach (var (_, type) in input.Types)
    {
      type.TypeDependencies = type.EnumerateAllTypeReferences()
        .Select(r => r.Name)
        .Distinct()
        .Where(n => char.IsUpper(n[0]))
        .ToImmutableArray();
    }

    return ITransform.TransformResult.NoChanges;
  }
}