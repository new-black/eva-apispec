using System.CommandLine;
using EVA.SDK.Generator.V2.Inputs;

namespace EVA.SDK.Generator.V2.Commands.ApiVersion;

public static class ApiVersionCommand
{
  public static void Register(Command command)
  {
    var listAssembliesCommand = new Command("api-version");
    command.Add(listAssembliesCommand);

    listAssembliesCommand.AddOption(SharedOptions.Input);
    listAssembliesCommand.SetHandler(async src =>
    {
      var model = await InputFactory.GetInputFromString(src).Read(true);
      Console.WriteLine(model.ApiVersion);
    }, SharedOptions.Input);
  }
}