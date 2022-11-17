using System.CommandLine;
using EVA.SDK.Generator.V2.Commands.ApiVersion;
using EVA.SDK.Generator.V2.Commands.Generate;
using EVA.SDK.Generator.V2.Commands.ListAssemblies;
using EVA.SDK.Generator.V2.Commands.ListVersions;
using EVA.SDK.Generator.V2.Commands.Update;

var command = new RootCommand("The EVA SDK Suite");

GenerateCommand.Register(command);
ListAssembliesCommand.Register(command);
ListVersionsCommand.Register(command);
ApiVersionCommand.Register(command);
UpdateCommand.Register(command);

await command.InvokeAsync(args);