using System.CommandLine;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.ApiVersion;

public class ApiVersionCommand
{
  public static void Register(Command command)
  {
    var listAssembliesCommand = new Command("api-version");
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
      var model = await input.Read(true);
      Console.WriteLine(model.ApiVersion);
    }, inputOption);
  }
}