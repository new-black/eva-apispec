using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs;

public interface IOutput<T>
{
  string? OutputPattern { get; }
  string[] ForcedRemoves { get; }
  Task Write(ApiDefinitionModel input, T options, OutputWriter writer);
}

public record OutputContext<T>(ApiDefinitionModel Input, T Options, OutputWriter Writer, ILogger Logger);