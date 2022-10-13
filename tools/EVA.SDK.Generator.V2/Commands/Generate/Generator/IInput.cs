using EVA.Core.Typings.V2;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator;

public interface IInput
{
  Task<ApiDefinitionModel> Read();
}