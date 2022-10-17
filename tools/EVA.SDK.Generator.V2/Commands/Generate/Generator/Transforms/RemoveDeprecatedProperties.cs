using System.Collections.Immutable;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Transforms;

public class RemoveDeprecatedProperties : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var changes = ITransform.TransformResult.NoChanges;

    foreach (var (_, type) in input.Types)
    {
      if (type.Properties?.Values.Any(p => p.Deprecated != null) ?? false)
      {
        type.Properties = type.Properties.Where(kv => kv.Value.Deprecated == null).ToImmutableSortedDictionary();
        changes = ITransform.TransformResult.Changes;
      }
    }

    return changes;
  }
}