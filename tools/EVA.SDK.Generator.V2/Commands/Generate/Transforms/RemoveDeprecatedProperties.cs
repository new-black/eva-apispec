using System.Collections.Immutable;
using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class RemoveDeprecatedProperties : INamedTransform
{
  public string ID => "remove-deprecated-properties";
  public string Description => "Will remove all deprecated properties";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var changes = ITransform.TransformResult.None;

    foreach (var (_, type) in input.Types)
    {
      if (type.Properties.Values.All(p => p.Deprecated == null)) continue;

      type.Properties = type.Properties.Where(kv => kv.Value.Deprecated == null).ToImmutableSortedDictionary();
      changes = ITransform.TransformResult.Changes;
    }

    return changes;
  }
}