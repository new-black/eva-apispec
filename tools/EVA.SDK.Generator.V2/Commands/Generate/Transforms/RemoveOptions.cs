﻿using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class RemoveOptions : INamedTransform
{
  public string ID => "remove-options";
  public string Description => "Removes options (properties that might contain multiple distinct types). Only the shared signature will remain.";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var changes = ITransform.TransformResult.None;
    foreach (var typeReference in input.EnumerateAllTypeReferences())
    {
      if (typeReference is not { Name: ApiSpecConsts.Specials.Option, Shared: not null }) continue;

      // If the shared object does not have any objects, and does not extend any other object. Flatten this to an generic object
      if (typeReference.Shared.Name is ApiSpecConsts.Object or ApiSpecConsts.Any)
      {
        typeReference.Arguments = ImmutableArray<TypeReference>.Empty;
        typeReference.Nullable = typeReference.Shared.Nullable;
        typeReference.Name = typeReference.Shared.Name;
        typeReference.Shared = null;
      }
      else
      {
        var sharedSpec = input.Types[typeReference.Shared.Name];
        if (sharedSpec is { Properties.Count: 0, Extends: null })
        {
          typeReference.Name = ApiSpecConsts.Object;
          typeReference.Arguments = ImmutableArray<TypeReference>.Empty;
          typeReference.Nullable = typeReference.Shared.Nullable;
          typeReference.Shared = null;
        }
        else
        {
          typeReference.Name = typeReference.Shared.Name;
          typeReference.Arguments = typeReference.Shared.Arguments;
          typeReference.Nullable = typeReference.Shared.Nullable;
          typeReference.Shared = null;
        }
      }

      changes = ITransform.TransformResult.StructuralChanges;
    }

    return changes;
  }
}