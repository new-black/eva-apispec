using System.CommandLine;

namespace EVA.SDK.Generator.V2.Helpers;

internal static class CommandExtensions
{
  internal static void AddOptions(this Command command, IEnumerable<Option> options)
  {
    foreach (var opt in options)
    {
      command.AddOption(opt);
    }
  }
}