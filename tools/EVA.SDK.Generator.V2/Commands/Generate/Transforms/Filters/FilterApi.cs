using System.Collections.Immutable;
using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms.Filters;

internal class FilterApi : ITransform
{
  public string Description => "Filters the API";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    if (options.Api == "eva")
    {
      // Only filter the services
      input.Services = input.Services.Where(s => s.Api == null).ToImmutableArray();
      return ITransform.TransformResult.StructuralChanges;
    }

    // Really filter on a specific api
    var name1 = options.Api;
    var name2 = options.Api + "-callbacks";
    input.Services = input.Services.Where(s => s.Api == name1 || s.Api == name2).ToImmutableArray();
    input.Errors = ImmutableArray<ErrorSpecification>.Empty;
    input.DatalakeExports = ImmutableArray<DatalakeExportTarget>.Empty;
    input.EventTargets = ImmutableArray<EventTarget>.Empty;

    // Type filtering is automatic
    return ITransform.TransformResult.StructuralChanges;
  }
}