using System.CommandLine;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.ListAssemblies;

public class ListAssembliesCommand
{
  public static void Register(Command command)
  {
    var listAssembliesCommand = new Command("list-assemblies");
    command.Add(listAssembliesCommand);
    var inputOption = new Option<string>(
      name: "--in",
      description: "The source of the input file. If not specified, will download the latest available."
    );
    inputOption.AddAlias("-i");
    listAssembliesCommand.AddOption(inputOption);
    listAssembliesCommand.SetHandler(async src =>
    {
      var input = InputFactory.GetInputFromString(src);
      var model = await input.Read();
      foreach (var assembly in model.EnumerateAllAssemblies())
      {
        Console.WriteLine($"- {assembly}");
      }
    }, inputOption);
  }
}