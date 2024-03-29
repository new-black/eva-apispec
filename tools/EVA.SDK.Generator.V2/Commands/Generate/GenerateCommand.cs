﻿using System.CommandLine;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.evaspec;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi_azureconnector;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate;

internal static class GenerateCommand
{
  internal static void Register(Command command)
  {
    var generateCommand = new Command("generate");
    command.Add(generateCommand);

    AddOutput<EvaSpecOptions, EvaSpecOptionsBinder, EvaSpecOutput>(generateCommand, "evaspec");
    AddOutput<OpenApiOptions, OpenApiOptionsBinder, OpenApiOutput>(generateCommand, "openapi");
    AddOutput<ApiDocsOptions, ApiDocsOptionsBinder, ApiDocsOutput>(generateCommand, "apidocs", hidden: true);
    AddOutput<OpenApiAzureConnectorOptions, OpenApiAzureConnectorOptionsBinder, OpenApiAzureConnectorOutput>(generateCommand, "openapi-azureconnector");
    AddOutput<SwiftOptions, SwiftOptionsBinder, SwiftOutput>(generateCommand, "swift");
    AddOutput<DotNetOptions, DotNetOptionsBinder, DotNetOutput>(generateCommand, "dotnet");
    AddOutput<TypescriptOptions, TypescriptOptionsBinder, TypescriptOutput>(generateCommand, "typescript");
    // AddOutput<ZodOptions, ZodOptionsBinder, ZodOutput>(generateCommand, "zod");
  }

  private static void AddOutput<T, TBinder, TOutput>(Command generateCommand, string name, bool hidden = false)
    where TBinder : BaseGenerateOptionsBinder<T>, new() where T : GenerateOptions, new() where TOutput : IOutput<T>, new()
  {
    var binder = new TBinder();
    var output = new TOutput();

    var description = $"Generate {name} typings.";
    var forcedTransformations = output.ForcedTransformations;
    if (forcedTransformations.Any())
    {
      description += $" Applies the following transformations by default: {string.Join(", ", forcedTransformations)}";
    }

    var command = new Command(name) { Description = description, IsHidden = hidden };
    command.AddOptions(binder.GetAllOptions(forcedTransformations));

    generateCommand.AddCommand(command);

    command.SetHandler(async (outputOptions, logger) => { await GenerationPipeline.Run(outputOptions, output, logger); }, binder, LogBinder.Instance);
  }
}