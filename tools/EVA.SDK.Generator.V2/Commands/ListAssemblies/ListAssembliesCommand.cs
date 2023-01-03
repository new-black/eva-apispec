using System.CommandLine;
using EVA.SDK.Generator.V2.Helpers;
using EVA.SDK.Generator.V2.Inputs;

namespace EVA.SDK.Generator.V2.Commands.ListAssemblies;

internal static class ListAssembliesCommand
{
  internal static void Register(Command command)
  {
    var listAssembliesCommand = new Command("list-assemblies");
    command.Add(listAssembliesCommand);

    listAssembliesCommand.AddOption(SharedOptions.Input);
    listAssembliesCommand.SetHandler(async (src, logger) =>
    {
      var model = await InputFactory.GetInputFromString(src).Read(logger);
      foreach (var assembly in model.EnumerateAllAssemblies())
      {
        Console.WriteLine($"- {assembly}");
      }
    }, SharedOptions.Input, LogBinder.Instance);
  }
}