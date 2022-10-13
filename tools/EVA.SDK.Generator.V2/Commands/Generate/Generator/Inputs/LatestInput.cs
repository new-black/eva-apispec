namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;

public class LatestInput : BaseGithubInput
{
  protected override string GetUrl()
  {
    var url = HttpConstants.LatestSpecUrl;
    Console.WriteLine($"Downloading latest version from: {HttpConstants.LatestSpecUrl}");
    return url;
  }
}