using System.IO.Enumeration;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms.Filters;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms.Internal;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;
using EVA.SDK.Generator.V2.Inputs;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate;

internal static class GenerationPipeline
{
  internal static readonly INamedTransform[] Transforms =
  {
    new FlattenGenerics(),
    new RemoveDeprecatedProperties(),
    new RemoveDeprecatedServices(),
    new RemoveEmptyTypes(),
    new RemoveErrors(),
    new RemoveEventExports(),
    new RemoveInheritance(),
    new RemoveNestedTypes(),
    new RemoveOptions(),
    new RemoveSharedRequestResponseTypes(),
    new RemoveUnusedGenericArguments(),
    new RemoveDataLakeExports()
  };

  private static readonly ITransform Transform1 = new FixDependencies();
  private static readonly ITransform Transform2 = new RemoveUnusedTypes();

  internal static async Task Run<T>(T opt, IOutput<T> output, ILogger logger) where T : GenerateOptions
  {
    // Some outputs require certain options
    foreach (var o in output.ForcedRemoves) opt.EnsureRemove(o);

    // Find all parts of the pipeline
    var input = InputFactory.GetInputFromString(opt.Input);
    var transforms = FindTransforms(opt).ToList();
    var filters = FindFilters(opt).ToList();

    // The input
    var model = await input.Read(logger);

    // Run filters (once)
    foreach (var filter in filters) filter.Transform(model, opt, logger);

    // Run transformations
    logger.LogInformation("Running transformations: {Transforms}", string.Join(", ", transforms.Select(t => t.Name)));
    for (var i = 0; i < 10; i++)
    {
      logger.LogDebug("\\nRunning iteration {Iteration}", i);
      var changes = ITransform.TransformResult.None;

      foreach (var transform in transforms)
      {
        var result = transform.Transform(model, opt, logger);
        result |= Transform1.Transform(model, opt, logger);
        result |= Transform2.Transform(model, opt, logger);

        logger.LogDebug("Transform {Transform} returned {Result}", transform.GetType().Name, result);

        // Clone model if needed
        if (result.HasFlag(ITransform.TransformResult.StructuralChanges))
        {
          logger.LogTrace("Cloning model");
          model = JsonContext.Default.ApiDefinitionModel.Clone(model);
        }

        changes |= result;
      }

      if (changes == ITransform.TransformResult.None) break;
    }

    // Target directory handling
    EnsureEmptyOutputFolderExists(opt, output.OutputPattern, logger);

    // Output
    var writer = new OutputWriter(opt.OutputDirectory);
    await output.Write(new OutputContext<T>(model, opt, writer, logger));
    logger.LogInformation("{WriterReport}", writer.ToReport());
  }

  /// <summary>
  /// This method will ensure the output path exists and is an empty directory.
  /// </summary>
  /// <exception cref="Exception"></exception>
  private static void EnsureEmptyOutputFolderExists(GenerateOptions opt, string? outputPattern, ILogger logger)
  {
    var fullOutputPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), opt.OutputDirectory));
    opt.OutputDirectory = fullOutputPath;
    if (opt.Overwrite)
    {
      if (outputPattern == null)
      {
        if (File.Exists(fullOutputPath)) File.Delete(fullOutputPath);
        if (Directory.Exists(fullOutputPath)) Directory.Delete(fullOutputPath, true);
        Directory.CreateDirectory(fullOutputPath);
      }
      else
      {
        if (!Directory.Exists(fullOutputPath)) Directory.CreateDirectory(fullOutputPath);
        var allFiles = Directory.GetFiles(fullOutputPath, outputPattern, SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
          var relativeFile = Path.GetRelativePath(fullOutputPath, file);
          if (FileSystemName.MatchesSimpleExpression(outputPattern, relativeFile)) File.Delete(file);
        }
      }
    }
    else
    {
      if (File.Exists(fullOutputPath)) throw new SdkException($"Cannot output to {fullOutputPath}, it is a file");
      if (Directory.Exists(fullOutputPath) && Directory.GetFileSystemEntries(fullOutputPath).Any()) throw new SdkException($"Cannot output to {fullOutputPath}, it is not empty");
      Directory.CreateDirectory(fullOutputPath);
    }

    logger.LogInformation("Outputting to: {FullOutputPath}", fullOutputPath);
  }


  private static IEnumerable<ITransform> FindFilters(GenerateOptions options)
  {
    if (options.FilterAssemblies is { Length: > 0 })
    {
      yield return new FilterAssemblies(options.FilterAssemblies);
    }

    if (options.FilterServices is { Length: > 0 })
    {
      yield return new FilterServices(options.FilterServices);
    }
  }

  private static IEnumerable<INamedTransform> FindTransforms(GenerateOptions options)
  {
    var remove = (options.Remove ?? new List<string>()).Distinct().ToHashSet();

    foreach (var transform in Transforms)
    {
      if (remove.Contains(transform.Name)) yield return transform;
    }
  }
}