namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;

public class LatestInput : BaseGithubInput
{
  protected override string GetUrl(bool quiet)
  {
    var url = HttpConstants.LatestSpecUrl;
    if(!quiet) Console.WriteLine($"Downloading latest version from: {HttpConstants.LatestSpecUrl}");
    return url;
  }
}