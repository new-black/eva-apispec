using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Generator.Transforms;

public class RemoveUnusedGenericArguments : ITransform
{
  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var changes = ITransform.TransformResult.NoChanges;

    while (true)
    {
      bool hasChanges = false;

      foreach (var (id, type) in input.Types)
      {
        if (type.TypeArguments == null) continue;
        if (!type.TypeArguments.Any()) continue;

        // Start by assuming they are all unused, they can only be acquitted by a reference
        var unusedArguments = type.TypeArguments.ToHashSet();
        foreach (var reference in type.EnumerateAllTypeReferences())
        {
          unusedArguments.Remove(reference.Name);
        }

        // Map to indices
        var unusedIndices = unusedArguments.Select(a => type.TypeArguments.IndexOf(a)).OrderByDescending(x => x).ToList();
        foreach (var unusedIndex in unusedIndices)
        {
          hasChanges = true;
          changes = ITransform.TransformResult.Changes;
          Console.WriteLine($"[TRIM]: Argument {type.TypeArguments[unusedIndex]} from {id}");
          type.TypeArguments = type.TypeArguments.WithoutIndex(unusedIndex, true);

          foreach (var reference in input.EnumerateAllTypeReferences())
          {
            if (reference.Name == id)
            {
              reference.Arguments = reference.Arguments.WithoutIndex(unusedIndex, true);
            }
          }
        }
      }

      if (!hasChanges) break;
    }

    return changes;
  }
}