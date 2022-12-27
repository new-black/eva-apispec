using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Inputs;

internal interface IInput
{
  Task<ApiDefinitionModel> Read(bool quiet = false);
}