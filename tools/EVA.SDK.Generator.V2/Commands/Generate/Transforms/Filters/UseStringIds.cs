using System.Collections.Immutable;
using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class UseStringIds : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var changes = ITransform.TransformResult.None;

    if (!options.UseStringIDs) return changes;

    foreach (var type in input.Types.Values)
    {
      foreach (var property in type.Properties.Values)
      {
        if (property.DataModelInformation != null)
        {
          if (property.Type.Name != ApiSpecConsts.String)
          {
            property.Type = new TypeReference(ApiSpecConsts.String, ImmutableArray<TypeReference>.Empty, property.Type.Nullable);
            changes = ITransform.TransformResult.Changes;
          }
        }
      }
    }

    return changes;
  }
}