using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs;

internal interface IOutput<T>
{
  string? OutputPattern { get; }
  string[] ForcedRemoves { get; }
  Task Write(OutputContext<T> ctx);
}

internal record OutputContext<T>(ApiDefinitionModel Input, T Options, OutputWriter Writer, ILogger Logger);