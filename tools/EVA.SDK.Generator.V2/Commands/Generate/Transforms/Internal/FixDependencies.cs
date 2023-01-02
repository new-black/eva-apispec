using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms.Internal;

public class FixDependencies : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
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