using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator;

public interface IInput
{
  Task<ApiDefinitionModel> Read(bool quiet = false);
}