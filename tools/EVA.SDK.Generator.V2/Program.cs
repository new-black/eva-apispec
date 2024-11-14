using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
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
  public static async Task Main(string[] args)
  {
    // Build the command
    var command = new RootCommand("The EVA SDK Suite");
    command.AddGlobalOption(SharedOptions.LogLevel.Option);

    GenerateCommand.Register(command);
    ListAssembliesCommand.Register(command);
    ListVersionsCommand.Register(command);
    ApiVersionCommand.Register(command);
    UpdateCommand.Register(command);

    // Add exception handler
    var builder = new CommandLineBuilder(command);
    builder.AddMiddleware(async (ctx, next) =>
    {
      ((IValueSource)LogBinder.Instance).TryGetValue(LogBinder.Instance, ctx.BindingContext, out var loggerObj);
      var logger = loggerObj as ILogger ?? throw new InvalidOperationException("Could not create a logger");
      try
      {
        await next(ctx);
      }
      catch (SdkException ex)
      {
        logger.LogError("An error occurred: {Message}", ex.Message);
        ctx.ExitCode = 1;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An unexpected error occurred");
        ctx.ExitCode = 1;
      }
    });
    builder.UseDefaults();
    await builder.Build().InvokeAsync(args);
  }
}