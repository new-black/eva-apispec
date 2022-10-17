using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Transforms;

public class RemoveEmptyTypes : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var whitelistedIDs = input.Services.Select(s => s.RequestTypeID).Concat(input.Services.Select(s => s.ResponseTypeID)).ToHashSet();
    var changes = ITransform.TransformResult.NoChanges;

    while (true)
    {
      var typesToRemove = new HashSet<string>();

      foreach (var (id, type) in input.Types)
      {
        if (type.Properties != null && type.Properties.Any()) continue;
        if (type.EnumValues != null) continue;
        if (whitelistedIDs.Contains(id)) continue;

        Console.WriteLine($"[TRIM]: Useless type {id}");

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
        changes = ITransform.TransformResult.Changes;
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