using System.Collections.Immutable;
using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class RemoveDataLakeExports : INamedTransform
{
  public string ID => "remove-datalake-exports";
  public string Description => "Will remove all type information regarding datalake exports";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    if (!input.DatalakeExports.Any()) return ITransform.TransformResult.None;

    input.DatalakeExports = ImmutableArray<DatalakeExportTarget>.Empty;
    return ITransform.TransformResult.Changes;
  }
}