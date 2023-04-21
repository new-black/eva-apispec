using System.Text.RegularExpressions;

namespace EVA.SDK.Generator.V2.Inputs;

internal static partial class InputFactory
{
  internal static IInput GetInputFromString(string? input)
  {
    if (input is "latest" or null) return new LatestInput();

    var matches = VersionRegex().Matches(input);
    if (matches.Any())
    {
      var version = matches.First().Value;
      return new SpecificVersionInput(version);
    }

    if (input.StartsWith("https://") || input.StartsWith("http://")) return new HttpInput(input);
    return new FileInput(input);
  }

  [GeneratedRegex("^[v]?(\\d+.\\d+.\\d+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
  private static partial Regex VersionRegex();
}