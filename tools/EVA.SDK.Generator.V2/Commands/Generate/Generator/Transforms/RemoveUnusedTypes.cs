using System.Collections.Immutable;
using EVA.Core.Typings.V2;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Transforms;

public class RemoveUnusedTypes : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var allUnusedTypes = input.Types.Keys.ToHashSet();
    var typesToIterate = new HashSet<string>(input.Services.Select(s => s.RequestTypeID).Concat(input.Services.Select(s => s.ResponseTypeID)));
    var allUsedTypes = new HashSet<string>();

    while (typesToIterate.Any())
    {
      var element = typesToIterate.First();
      typesToIterate.Remove(element);

      if (allUsedTypes.Contains(element)) continue;

      allUsedTypes.Add(element);
      allUnusedTypes.Remove(element);

      foreach (var t in input.Types[element].TypeDependencies)
      {
        typesToIterate.Add(t);
      }
    }

    if (allUnusedTypes.Any())
    {
      Console.WriteLine($"[TRANSFORM]: Removed {allUnusedTypes.Count} unused types");
      input.Types = input.Types.Where(kv => !allUnusedTypes.Contains(kv.Key)).ToImmutableSortedDictionary();
      return ITransform.TransformResult.Changes;
    }
    else
    {
      return ITransform.TransformResult.NoChanges;
    }
  }
}