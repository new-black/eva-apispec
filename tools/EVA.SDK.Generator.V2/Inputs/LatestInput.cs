namespace EVA.SDK.Generator.V2.Inputs;

internal class LatestInput : BaseGithubInput
{
  protected override string GetUrl(bool quiet)
  {
    var url = HttpConstants.LatestSpecUrl;
    if(!quiet) Console.WriteLine($"Downloading latest version from: {HttpConstants.LatestSpecUrl}");
    return url;
  }
}