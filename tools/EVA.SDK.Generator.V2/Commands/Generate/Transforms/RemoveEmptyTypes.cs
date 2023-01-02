using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

public class RemoveEmptyTypes : INamedTransform
{
  public string Name => "empty-types";
  public string Description => "Will remove all types that don't have any properties";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var whitelistedIDs = input.Services.Select(s => s.RequestTypeID).Concat(input.Services.Select(s => s.ResponseTypeID)).ToHashSet();
    var changes = ITransform.TransformResult.NoChanges;

    while (true)
    {
      var typesToRemove = new HashSet<string>();

      foreach (var (id, type) in input.Types)
      {
        if (type.Properties.Any()) continue;
        if (type.EnumValues != null) continue;
        if (whitelistedIDs.Contains(id)) continue;

        var redirect = type.Extends ?? new TypeReference("object", ImmutableArray<TypeReference>.Empty, false);
        typesToRemove.Add(id);

        foreach (var reference in input.EnumerateAllTypeReferences())
        {
          if (reference.Name == id)
          {
            reference.Name = redirect.Name;
            reference.Arguments = redirect.Arguments;
          }
        }

        break;
      }

      if (typesToRemove.Any())
      {
        changes = ITransform.TransformResult.StructuralChanges;
        input.Types = input.Types.Where(kv => !typesToRemove.Contains(kv.Key)).ToImmutableSortedDictionary();
      }
      else
      {
        break;
      }
    }

    // Fix extensions
    foreach (var (_, type) in input.Types)
    {
      if (type.Extends is { Name: "object" }) type.Extends = null;
    }

    return changes;
  }
}