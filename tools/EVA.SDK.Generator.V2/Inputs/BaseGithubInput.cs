using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Inputs;

internal abstract class BaseGithubInput : IInput
{
  protected abstract string GetUrl(ILogger logger);

  public async Task<ApiDefinitionModel> Read(ILogger logger)
  {
    return await HttpHelpers.GetJson(GetUrl(logger), JsonContext.Default.ApiDefinitionModel, logger);
  }
}