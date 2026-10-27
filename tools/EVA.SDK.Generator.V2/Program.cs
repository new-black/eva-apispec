using System.CommandLine;
using System.CommandLine.Invocation;
using EVA.SDK.Generator.V2.Commands.ApiVersion;
using EVA.SDK.Generator.V2.Commands.Generate;
using EVA.SDK.Generator.V2.Commands.ListAssemblies;
using EVA.SDK.Generator.V2.Commands.ListVersions;
using EVA.SDK.Generator.V2.Commands.Update;
using EVA.SDK.Generator.V2.Exceptions;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2;

public static class Program
{
  public static async Task<int> Main(string[] args)
  {
    // Build the command
    var command = new RootCommand("The EVA SDK Suite");
    command.Add(SharedOptions.LogLevel.Option);

    GenerateCommand.Register(command);
    ListAssembliesCommand.Register(command);
    ListVersionsCommand.Register(command);
    ApiVersionCommand.Register(command);
    UpdateCommand.Register(command);

    var parseResult = command.Parse(args);

    // Let the parser report invalid command lines before we read any option values.
    if (parseResult.Errors.Count > 0)
    {
      return await parseResult.InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
    }

    var logger = LogBinder.Create(parseResult);

    // Add exception handler
    try
    {
      return await parseResult.InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
    }
    catch (SdkException ex)
    {
      logger.LogError("An error occurred: {Message}", ex.Message);
      return 1;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An unexpected error occurred");
      return 1;
    }
  }
}
