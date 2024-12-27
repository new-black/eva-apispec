using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms.Internal;

internal class FixDependencies : ITransform
{
  public string Description => "Fixes dependencies";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    foreach (var (_, type) in input.Types)
    {
      type.TypeDependencies = type.EnumerateAllTypeReferences()
        .Select(r => r.Name)
        .Concat(type.ParentType == null ? Array.Empty<string>() : new[] { type.ParentType })
        .Distinct()
        .Where(n => char.IsUpper(n[0]))
        .ToImmutableArray();
    }

    return ITransform.TransformResult.None;
  }
}