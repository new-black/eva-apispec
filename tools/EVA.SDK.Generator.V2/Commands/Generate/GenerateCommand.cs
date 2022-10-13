using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Commands.Generate.Generator;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.evaspec;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.openapi;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.swift;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate;

public class GenerateCommand
{
  public static void Register(Command command)
  {
    var generateCommand = new Command("generate");
    command.Add(generateCommand);
    var optionsBinder = new GenerateOptionsBinder();
    generateCommand.AddGlobalOptions(optionsBinder);

    AddOutput<EvaSpecOptions, EvaSpecOptionsBinder>(generateCommand, optionsBinder, "evaspec", new EvaSpecOptionsBinder(), o => new EvaSpecOutput(o));
    AddOutput<OpenApiOptions, OpenApiOptionsBinder>(generateCommand, optionsBinder, "openapi", new OpenApiOptionsBinder(), o => new OpenApiOutput(o));
    AddOutput<SwiftOptions, SwiftOptionsBinder>(generateCommand, optionsBinder, "swift", new SwiftOptionsBinder(), o => new SwiftOutput(o));
  }

  private static void AddOutput<T, TBinder>(Command generateCommand, GenerateOptionsBinder optionsBinder, string name, TBinder binder, Func<T, IOutput> outputBuilder)
    where TBinder : BinderBase<T>, IOptionProvider
  {
    var command = new Command(name);
    generateCommand.AddCommand(command);
    command.AddOptions(binder);
    command.SetHandler(async (generateOptions, outputOptions) =>
    {
      try
      {
        await GenerationRunner.Run(generateOptions, outputBuilder(outputOptions));
      }
      catch (Exception ex)
      {
        await Console.Error.WriteLineAsync("[ERROR]: " + ex.Message);
        throw;
      }
    }, optionsBinder, binder);
  }
}