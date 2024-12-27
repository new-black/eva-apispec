using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class RemoveInheritance : INamedTransform
{
  public string ID => "remove-inheritance";
  public string Description => "Will remove inheritance by flattening type hierarchies into a single type.";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var changes = ITransform.TransformResult.None;

    foreach (var (_, type) in input.Types)
    {
      while (type.Extends != null)
      {
        if (!char.IsUpper(type.Extends.Name[0]))
        {
          logger.LogWarning("How can I merge {ExtendsName} into {TypeName}?", type.Extends.Name, type.TypeName);
          break;
        }

        changes = ITransform.TransformResult.StructuralChanges;

        var baseType = !type.Extends.Arguments.Any() ? input.Types[type.Extends.Name] : input.Types[type.Extends.Name].TemplateWith(type.Extends.Arguments);
        var propsToAdd = baseType.Properties.Where(kv => !type.Properties.ContainsKey(kv.Key)).ToList();
        type.Properties = type.Properties.Concat(propsToAdd).ToImmutableSortedDictionary();
        type.Extends = baseType.Extends;
      }
    }

    return changes;
  }
}