using System.CommandLine;
using EVA.SDK.Generator.V2.Helpers;
using EVA.SDK.Generator.V2.Inputs;
using Microsoft.Extensions.Logging.Abstractions;

namespace EVA.SDK.Generator.V2.Commands.ApiVersion;

internal static class ApiVersionCommand
{
  internal static void Register(Command command)
  {
    var listAssembliesCommand = new Command("api-version");
    command.Add(listAssembliesCommand);

    listAssembliesCommand.Add(SharedOptions.Input);
    listAssembliesCommand.SetAction(async (parseResult, _) =>
    {
      var src = SharedOptions.Input.Value(parseResult);
      var model = await InputFactory.GetInputFromString(src).Read(NullLogger.Instance);
      Console.WriteLine(model.ApiVersion);
    });
  }
}