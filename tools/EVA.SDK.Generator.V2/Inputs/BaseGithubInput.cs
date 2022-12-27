using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Inputs;

internal abstract class BaseGithubInput : IInput
{
  protected abstract string GetUrl(bool quiet);

  public async Task<ApiDefinitionModel> Read(bool quiet = false)
  {
    return await HttpHelpers.GetJson(GetUrl(quiet), JsonContext.Default.ApiDefinitionModel);
  }
}