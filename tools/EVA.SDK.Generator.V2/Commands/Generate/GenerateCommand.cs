using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.evaspec;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.zod;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate;

public static class GenerateCommand
{
  public static void Register(Command command)
  {
    var generateCommand = new Command("generate");
    command.Add(generateCommand);

    var optionsBinder = new GenerateOptionsBinder();
    generateCommand.AddGlobalOptions(optionsBinder);

    AddOutput<EvaSpecOptions, EvaSpecOptionsBinder>(generateCommand, optionsBinder, "evaspec", o => new EvaSpecOutput(o));
    AddOutput<OpenApiOptions, OpenApiOptionsBinder>(generateCommand, optionsBinder, "openapi", o => new OpenApiOutput(o));
    AddOutput<SwiftOptions, SwiftOptionsBinder>(generateCommand, optionsBinder, "swift", o => new SwiftOutput(o));
    AddOutput<DotNetOptions, DotNetOptionsBinder>(generateCommand, optionsBinder, "dotnet", o => new DotNetOutput(o));
    AddOutput<TypescriptOptions, TypescriptOptionsBinder>(generateCommand, optionsBinder, "typescript", o => new TypescriptOutput(o));
    AddOutput<ZodOptions, ZodOptionsBinder>(generateCommand, optionsBinder, "zod", o => new ZodOutput(o));
  }

  private static void AddOutput<T, TBinder>(Command generateCommand, GenerateOptionsBinder optionsBinder, string name, Func<T, IOutput> outputBuilder)
    where TBinder : BinderBase<T>, IOptionProvider, new()
  {
    var command = new Command(name);
    var binder = new TBinder();
    generateCommand.AddCommand(command);
    command.AddOptions(binder);
    command.SetHandler(async (generateOptions, outputOptions) =>
    {
      try
      {
        await GenerationPipeline.Run(generateOptions, outputBuilder(outputOptions));
      }
      catch (Exception ex)
      {
        await Console.Error.WriteLineAsync("[ERROR]: " + ex.Message);
        throw;
      }
    }, optionsBinder, binder);
  }
}