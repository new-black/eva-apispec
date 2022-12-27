using System.CommandLine;
using EVA.SDK.Generator.V2.Helpers;
using EVA.SDK.Generator.V2.Inputs;

namespace EVA.SDK.Generator.V2.Commands.ListAssemblies;

public static class ListAssembliesCommand
{
  public static void Register(Command command)
  {
    var listAssembliesCommand = new Command("list-assemblies");
    command.Add(listAssembliesCommand);

    listAssembliesCommand.AddOption(SharedOptions.Input);
    listAssembliesCommand.SetHandler(async src =>
    {
      var model = await InputFactory.GetInputFromString(src).Read();
      foreach (var assembly in model.EnumerateAllAssemblies())
      {
        Console.WriteLine($"- {assembly}");
      }
    }, SharedOptions.Input);
  }
}