using EVA.Core.Typings.V2;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs;

public interface IOutput
{
  void FixOptions(GenerateOptions options);
  Task Write(ApiDefinitionModel input, string outputDirectory);
}