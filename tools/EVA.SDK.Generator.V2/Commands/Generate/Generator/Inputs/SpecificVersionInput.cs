namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;

public class SpecificVersionInput : BaseGithubInput
{
  private readonly string _version;

  public SpecificVersionInput(string version)
  {
    _version = version;
  }

  protected override string GetUrl(bool quiet)
  {
    var url = string.Format(HttpConstants.SpecificVersionUrl, _version);
    if(!quiet) Console.WriteLine($"Downloading version {_version} from: {url}");
    return url;
  }
}