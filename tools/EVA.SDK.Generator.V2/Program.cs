using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using EVA.SDK.Generator.V2.Commands.ApiVersion;
using EVA.SDK.Generator.V2.Commands.Generate;
using EVA.SDK.Generator.V2.Commands.ListAssemblies;
using EVA.SDK.Generator.V2.Commands.ListVersions;
using EVA.SDK.Generator.V2.Commands.Update;
using EVA.SDK.Generator.V2.Exceptions;

// Build the command
var command = new RootCommand("The EVA SDK Suite");
GenerateCommand.Register(command);
ListAssembliesCommand.Register(command);
ListVersionsCommand.Register(command);
ApiVersionCommand.Register(command);
UpdateCommand.Register(command);

// Add exception handler
var builder = new CommandLineBuilder(command);
builder.AddMiddleware(async (ctx, next) =>
{
  try
  {
    await next(ctx);
  }
  catch (SdkException ex)
  {
    await Console.Error.WriteLineAsync(ex.Message);
  }
  catch (Exception ex)
  {
    await Console.Error.WriteLineAsync("Something went wrong:");
    await Console.Error.WriteLineAsync(ex.ToString());
  }
});
builder.UseDefaults();
await builder.Build().InvokeAsync(args);