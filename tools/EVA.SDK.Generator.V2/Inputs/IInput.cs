using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Inputs;

internal interface IInput
{
  Task<ApiDefinitionModel> Read(ILogger logger);
}