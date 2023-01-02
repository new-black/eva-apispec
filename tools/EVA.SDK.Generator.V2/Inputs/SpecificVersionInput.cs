using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Inputs;

internal class SpecificVersionInput : BaseGithubInput
{
  private readonly string _version;

  internal SpecificVersionInput(string version)
  {
    _version = version;
  }

  protected override string GetUrl(ILogger logger)
  {
    var url = string.Format(HttpConstants.SpecificVersionUrl, _version);
    logger.LogInformation("Downloading spec {Version} from: {Url}", _version, url);
    return url;
  }
}