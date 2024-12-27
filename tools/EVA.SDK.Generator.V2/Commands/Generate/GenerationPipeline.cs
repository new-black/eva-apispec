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
  internal static INamedTransform[] AllTransforms =>
  [
    new RemoveGenerics(),
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
    new RemoveDataLakeExports(),
  ];

  private static readonly ITransform Transform1 = new FixDependencies();
  private static readonly ITransform Transform2 = new RemoveUnusedTypes();

  internal static async Task Run<T>(T opt, IOutput<T> output, ILogger logger) where T : GenerateOptions
  {
    // Find all parts of the pipeline
    var input = InputFactory.GetInputFromString(opt.Input);
    var transforms = FindTransforms(opt, output, opt).ToList();
    var filters = FindFilters(opt).ToList();

    // The input
    var model = await input.Read(logger);

    // Run filters (once)
    foreach (var filter in filters)
    {
      filter.Transform(model, opt, logger);
    }

    if (filters.Count > 0)
    {
      Transform1.Transform(model, opt, logger);
      Transform2.Transform(model, opt, logger);
    }

    // Other one-off transforms
    if (opt.UseStringIDs)
    {
      new UseStringIds().Transform(model, opt, logger);
    }

    // Run transformations
    for (var i = 0; i < 10; i++)
    {
      logger.LogDebug("Running iteration {Iteration}", i);
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

    // Orphaned types
    if (opt.OrphanedTypesAssembly != null)
    {
      var serviceAssemblies = model.Services.Select(s => s.Assembly).ToHashSet();
      foreach (var type in model.Types.Values)
      {
        if (!serviceAssemblies.Contains(type.Assembly)) type.Assembly = opt.OrphanedTypesAssembly;
      }

      foreach (var errors in model.Errors)
      {
        if (!serviceAssemblies.Contains(errors.Assembly)) errors.Assembly = opt.OrphanedTypesAssembly;
      }
    }

    // Target directory handling
    EnsureEmptyOutputFolderExists(opt, output.OutputPattern, logger);

    // Output
    var writer = new OutputWriter(opt.OutputDirectory, logger);
    await output.Write(new OutputContext<T>(model, opt, writer, logger));

    // Delete remaining
    writer.DeleteRemaining();

    // Report
    writer.WriteReport(logger);
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
      // Don't do anything here if we are overwriting.
    }
    else
    {
      if (File.Exists(fullOutputPath)) throw new SdkException($"Cannot output to {fullOutputPath}, it is a file");
      if (Directory.Exists(fullOutputPath) && Directory.GetFileSystemEntries(fullOutputPath).Length != 0) throw new SdkException($"Cannot output to {fullOutputPath}, it is not empty");
      if (!Directory.Exists(fullOutputPath)) Directory.CreateDirectory(fullOutputPath);
    }

    logger.LogInformation("Outputting to: {FullOutputPath}", fullOutputPath);
  }


  private static IEnumerable<ITransform> FindFilters(GenerateOptions options)
  {
    yield return new FilterApi();

    if (options.FilterAssemblies is { Length: > 0 } || options.MergeSmallAssemblies != null)
    {
      yield return new FilterAssemblies(options.FilterAssemblies, options.MergeSmallAssemblies, options.MergeSmallAssembliesLimit);
    }

    if (options.FilterServices is { Length: > 0 })
    {
      yield return new FilterServices(options.FilterServices);
    }
  }

  private static IEnumerable<INamedTransform> FindTransforms<T>(GenerateOptions options, IOutput<T> output, T opt)
  {
    var remove = options.Transforms.Distinct().ToHashSet();
    foreach (var x in AllTransforms)
    {
      if (remove.Contains(x.ID) || output.GetForcedTransformations(opt, x)) yield return x;
    }

  }
}