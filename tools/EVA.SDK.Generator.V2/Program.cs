using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Generator;
using EVA.SDK.Generator.V2.Generator.Outputs;
using EVA.SDK.Generator.V2.Generator.Outputs.evaspec;
using EVA.SDK.Generator.V2.Generator.Outputs.openapi;
using EVA.SDK.Generator.V2.Generator.Outputs.swift;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2;

public class Program
{
  private static readonly RootCommand RootCommand;
  private static readonly Command GenerateCommand;
  private static readonly GenerateOptionsBinder OptionsBinder;

  static Program()
  {
    RootCommand = new RootCommand("The EVA SDK Suite");

    GenerateCommand = new Command("generate");
    RootCommand.Add(GenerateCommand);
    OptionsBinder = new GenerateOptionsBinder();
    GenerateCommand.AddGlobalOptions(OptionsBinder);

    var listAssembliesCommand = new Command("assemblies");
    RootCommand.Add(listAssembliesCommand);
    var inputOption = new Option<string>(
      name: "--in",
      description: "The source of the input file. If not specified, will download the latest available."
    );
    inputOption.AddAlias("-i");
    listAssembliesCommand.AddOption(inputOption);
    listAssembliesCommand.SetHandler(async src =>
    {
      var input = GenerationRunner.FindInput(src);
      var model = await input.Read();
      foreach (var assembly in model.EnumerateAllAssemblies())
      {
        Console.WriteLine($"- {assembly}");
      }
    }, inputOption);
  }

  public static async Task Main(string[] args)
  {
    AddOutput<EvaSpecOptions, EvaSpecOptionsBinder>("evaspec", new EvaSpecOptionsBinder(), o => new EvaSpecOutput(o));
    AddOutput<OpenApiOptions, OpenApiOptionsBinder>("openapi", new OpenApiOptionsBinder(), o => new OpenApiOutput(o));

    AddOutput<SwiftOptions, SwiftOptionsBinder>("swift", new SwiftOptionsBinder(), o => new SwiftOutput(o));

    await RootCommand.InvokeAsync(args);
  }

  private static void AddOutput<T, TBinder>(string name, TBinder binder, Func<T, IOutput> outputBuilder) where TBinder : BinderBase<T>, IOptionProvider
  {
    var command = new Command(name);
    GenerateCommand.AddCommand(command);
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
    }, OptionsBinder, binder);
  }

  private static void AddOutput(string name, IOutput output)
  {
    var command = new Command(name);
    GenerateCommand.AddCommand(command);
    command.SetHandler(async generateOptions =>
    {
      try
      {
        await GenerationRunner.Run(generateOptions, output);
      }
      catch (Exception ex)
      {
        await Console.Error.WriteLineAsync("[ERROR]: " + ex.Message);
        throw;
      }
    }, OptionsBinder);
  }
}