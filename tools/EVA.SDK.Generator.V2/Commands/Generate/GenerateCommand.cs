using System.CommandLine;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.evaspec;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.zod;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate;

internal static class GenerateCommand
{
  internal static void Register(Command command)
  {
    var generateCommand = new Command("generate");
    command.Add(generateCommand);

    AddOutput<EvaSpecOptions, EvaSpecOptionsBinder, EvaSpecOutput>(generateCommand, "evaspec", null);
    AddOutput<OpenApiOptions, OpenApiOptionsBinder, OpenApiOutput>(generateCommand, "openapi", null);
    AddOutput<ApiDocsOptions, ApiDocsOptionsBinder, ApiDocsOutput>(generateCommand, "apidocs", new OpenApi.Outputs.apidocs.ApiDocsOutput(), hidden: true);
    AddOutput<SwiftOptions, SwiftOptionsBinder, SwiftOutput>(generateCommand, "swift", new OpenApi.Outputs.swift.SwiftOutput());
    AddOutput<DotNetOptions, DotNetOptionsBinder, DotNetOutput>(generateCommand, "dotnet", new OpenApi.Outputs.dotnet.DotNetOutput());
    AddOutput<TypescriptOptions, TypescriptOptionsBinder, TypescriptOutput>(generateCommand, "typescript", new OpenApi.Outputs.typescript.TypescriptOutput());
    AddOutput<ZodOptions, ZodOptionsBinder, ZodOutput>(generateCommand, "zod", new OpenApi.Outputs.zod.ZodOutput());
  }

  /// <param name="generateCommand">The command to add the output to.</param>
  /// <param name="name">The name of the output.</param>
  /// <param name="apiOutput">The output generated from an OpenAPI document of EVA (with --token or --openapi), if it supports that.</param>
  /// <param name="hidden">Whether the output is left out of the help.</param>
  private static void AddOutput<T, TBinder, TOutput>(Command generateCommand, string name, IApiOutput<T>? apiOutput, bool hidden = false)
    where TBinder : BaseGenerateOptionsBinder<T>, new() where T : GenerateOptions, new() where TOutput : IOutput<T>, new()
  {
    var binder = new TBinder();
    var output = new TOutput();

    var description = $"Generate {name} typings.";

    var opt = new T();
    bool CheckTransform(INamedTransform t) => output.GetForcedTransformations(opt, t);

    var forcedTransformations = GenerationPipeline.AllTransforms.Where(CheckTransform).ToList();
    if (forcedTransformations.Count != 0)
    {
      description += $" Applies the following transformations by default: {string.Join(", ", forcedTransformations.Select(x => x.ID))}";
    }

    var command = new Command(name) { Description = description, Hidden = hidden };
    command.AddOptions(binder.GetAllOptions(CheckTransform));

    generateCommand.Add(command);

    command.SetAction(async (parseResult, _) =>
    {
      var outputOptions = binder.Bind(parseResult);
      var logger = LogBinder.Create(parseResult);

      if (!outputOptions.FromOpenApi)
      {
        await GenerationPipeline.Run(outputOptions, output, logger);
      }
      else
      {
        if (apiOutput == null) throw new SdkException($"The {name} output cannot be generated from an OpenAPI document");
        await ApiGenerationPipeline.Run(outputOptions, apiOutput, logger);
      }
    });
  }
}