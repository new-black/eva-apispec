using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

public class RemoveInheritance : INamedTransform
{
  public string Name => "inheritance";
  public string Description => "Will flatten all type hierarchies to the concrete types";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var changes = ITransform.TransformResult.NoChanges;

    foreach (var (_, type) in input.Types)
    {
      while (type.Extends != null)
      {
        if (!char.IsUpper(type.Extends.Name[0]))
        {
          Console.WriteLine($"How can I merge {type.Extends.Name} into {type.TypeName}?");
          break;
        }

        changes = ITransform.TransformResult.StructuralChanges;

        TypeSpecification baseType;
        if (!type.Extends.Arguments.Any())
        {
          baseType = input.Types[type.Extends.Name];
        }
        else
        {
          baseType = input.Types[type.Extends.Name].TemplateWith(type.Extends.Arguments);
        }

        type.Properties = (type.Properties);
        var propsToAdd = baseType.Properties.Where(kv => !type.Properties.ContainsKey(kv.Key)).ToList();
        type.Properties = type.Properties.Concat(propsToAdd).ToImmutableSortedDictionary();
        type.Extends = baseType.Extends;
      }
    }

    return changes;
  }
}