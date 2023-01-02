using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Inputs;

internal class LatestInput : BaseGithubInput
{
  protected override string GetUrl(ILogger logger)
  {
    var url = HttpConstants.LatestSpecUrl;
    logger.LogInformation("Downloading latest spec from: {Url}", HttpConstants.LatestSpecUrl);
    return url;
  }
}