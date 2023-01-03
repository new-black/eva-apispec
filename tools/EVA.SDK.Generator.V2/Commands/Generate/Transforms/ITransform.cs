using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal interface ITransform
{
  [Flags]
  public enum TransformResult
  {
    StructuralChanges = 2,
    Changes = 1,
    None = 0
  }

  TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger);
}

internal interface INamedTransform : ITransform
{
  string Name { get; }
  string Description { get; }
}