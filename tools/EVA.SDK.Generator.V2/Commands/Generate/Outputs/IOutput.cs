using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs;

public interface IOutput<T>
{
  string? OutputPattern { get; }
  string[] ForcedRemoves { get; }
  Task Write(ApiDefinitionModel input, T options);
}