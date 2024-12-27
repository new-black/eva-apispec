﻿using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms.Internal;

internal class RemoveUnusedTypes : ITransform
{
  public string Description => "Removes unused types";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var allUnusedTypes = input.Types.Keys.ToHashSet();
    var typesToIterate = new HashSet<string>(
      input.Services.Select(s => s.RequestTypeID)
        .Concat(input.Services.Select(s => s.ResponseTypeID))
        .Concat(input.EventTargets.Select(e => e.DataType).FilterNotNull())
        .Concat(input.DatalakeExports.Select(e => e.DataType))
        .Concat(input.Errors.Select(e => e.DataTypeID).FilterNotNull()));
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

    if (!allUnusedTypes.Any()) return ITransform.TransformResult.None;

    input.Types = input.Types.Where(kv => !allUnusedTypes.Contains(kv.Key)).ToImmutableSortedDictionary();
    return ITransform.TransformResult.Changes;
  }
}