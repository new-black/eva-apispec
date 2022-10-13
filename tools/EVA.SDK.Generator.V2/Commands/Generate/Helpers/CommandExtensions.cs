using System.CommandLine;

namespace EVA.SDK.Generator.V2.Commands.Generate.Helpers;

public static class CommandExtensions
{
  public static void AddOptions(this Command command, IOptionProvider optionProvider)
  {
    foreach (var opt in optionProvider.GetAllOptions())
    {
      command.AddOption(opt);
    }
  }

  public static void AddGlobalOptions(this Command command, IOptionProvider optionProvider)
  {
    foreach (var opt in optionProvider.GetAllOptions())
    {
      command.AddGlobalOption(opt);
    }
  }
}