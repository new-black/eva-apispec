using System.Text.RegularExpressions;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;

public class InputFactory
{
  private static readonly Regex _versionRegex = new Regex(@"^[v]?(\d+.\d+.\d+)$", RegexOptions.Compiled);

  internal static IInput GetInputFromString(string input)
  {
    if (input is "latest" or null) return new LatestInput();

    var matches = _versionRegex.Matches(input);
    if (matches.Any())
    {
      var version = matches.First().Value;
      return new SpecificVersionInput(version);
    }

    if (input.StartsWith("https://") || input.StartsWith("http://")) return new HttpInput(input);
    return new FileInput(input);
  }
}