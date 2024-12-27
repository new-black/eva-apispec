using System.Collections.Immutable;
using System.Diagnostics;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class UseStringIds : ITransform
{
  public string Description => "Use string IDs instead of the ID type";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var changes = ITransform.TransformResult.None;

    foreach (var type in input.Types.Values)
    {
      foreach (var property in type.Properties.Values)
      {
        foreach (var reference in property.Type.EnumerateAllTypeReferences())
        {
          if (reference.Name is ApiSpecConsts.ID)
          {
            reference.Name = ApiSpecConsts.String;
            changes = ITransform.TransformResult.Changes;
          }
          else if (reference.Name == ApiSpecConsts.Specials.Map && reference.Arguments[0].Name is ApiSpecConsts.ID)
          {
            reference.Arguments[0].Name = ApiSpecConsts.String;
            changes = ITransform.TransformResult.Changes;
          }
        }
      }
    }

    return changes;
  }
}