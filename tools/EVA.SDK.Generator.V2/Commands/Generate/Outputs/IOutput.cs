using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs;

internal interface IOutput<T>
{
  string? OutputPattern { get; }
  bool GetForcedTransformations(T options, INamedTransform transform);
  Task Write(OutputContext<T> ctx);
}

internal record OutputContext<T>(ApiDefinitionModel Input, T Options, OutputWriter Writer, ILogger Logger);