using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class RemoveUnusedGenericArguments : INamedTransform
{
  public const string ID = "remove-unused-type-params";
  public string Name => ID;
  public string Description => "Removes unused generic type parameters";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var changes = ITransform.TransformResult.None;

    while (true)
    {
      var hasChanges = false;

      foreach (var (id, type) in input.Types)
      {
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
          changes = ITransform.TransformResult.StructuralChanges;
          type.TypeArguments = type.TypeArguments.WithoutIndex(unusedIndex);

          foreach (var reference in input.EnumerateAllTypeReferences())
          {
            if (reference.Name == id)
            {
              reference.Arguments = reference.Arguments.WithoutIndex(unusedIndex);
            }
          }
        }
      }

      if (!hasChanges) break;
    }

    return changes;
  }
}