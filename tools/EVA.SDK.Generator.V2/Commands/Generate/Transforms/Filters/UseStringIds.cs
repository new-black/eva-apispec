using System.Collections.Immutable;
using System.Diagnostics;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class UseStringIds : ITransform
{
  private readonly UseStringIDsMode _mode;

  public UseStringIds(UseStringIDsMode mode)
  {
    _mode = mode;
  }

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var changes = ITransform.TransformResult.None;

    foreach (var type in input.Types.Values)
    {
      foreach (var property in type.Properties.Values)
      {
        if (property.DataModelInformation == null)
        {
          foreach (var reference in property.Type.EnumerateAllTypeReferences())
          {
            if (reference.Name == ApiSpecConsts.Specials.Map && reference.Arguments[0].Name == ApiSpecConsts.Int64)
            {
              logger.LogWarning("{TypeName} is using a map with int64 as key without data model information", type.TypeName);
            }
          }
        }

        if (_mode == UseStringIDsMode.Optimistic || property.DataModelInformation != null)
        {
          foreach (var reference in property.Type.EnumerateAllTypeReferences())
          {
            if (reference.Name == ApiSpecConsts.Int64)
            {
              reference.Name = ApiSpecConsts.String;
              changes = ITransform.TransformResult.Changes;
            }
            else if (reference.Name == ApiSpecConsts.Specials.Map && reference.Arguments[0].Name == ApiSpecConsts.Int64)
            {
              reference.Arguments[0].Name = ApiSpecConsts.String;
              changes = ITransform.TransformResult.Changes;
            }
          }
        }
      }
    }

    return changes;
  }
}

internal enum UseStringIDsMode
{
  Default,
  Optimistic
}