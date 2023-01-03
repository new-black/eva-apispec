using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Inputs;

internal class LatestInput : BaseGithubInput
{
  protected override string GetUrl(ILogger logger)
  {
    logger.LogInformation("Downloading latest spec from: {Url}", HttpConstants.LatestSpecUrl);
    return HttpConstants.LatestSpecUrl;
  }
}