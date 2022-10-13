using System.Collections.Immutable;
using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Generator.Transforms;

public class RemoveInheritance : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var changes = ITransform.TransformResult.NoChanges;
    foreach (var (id, type) in input.Types)
    {
      while (type.Extends != null)
      {
        if (!char.IsUpper(type.Extends.Name[0]))
        {
          Console.WriteLine($"How can I merge {type.Extends.Name} into {type.TypeName}?");
          break;
        }

        changes = ITransform.TransformResult.Changes;

        TypeSpecification baseType;
        if (type.Extends.Arguments == null || !type.Extends.Arguments.Any())
        {
          baseType = input.Types[type.Extends.Name];
        }
        else
        {
          baseType = input.Types[type.Extends.Name].TemplateWith(type.Extends.Arguments);
        }

        type.Properties = (type.Properties ?? ImmutableSortedDictionary<string, PropertySpecification>.Empty);
        var propsToAdd = (baseType.Properties ?? ImmutableSortedDictionary<string, PropertySpecification>.Empty).Where(kv => !type.Properties.ContainsKey(kv.Key)).ToList();
        type.Properties = type.Properties.Concat(propsToAdd).ToImmutableSortedDictionary();
        type.Extends = baseType.Extends;
      }
    }

    return changes;
  }
}