using System.IO.Enumeration;
using System.Text.Json;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms.Filters;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms.Internal;
using EVA.SDK.Generator.V2.Helpers;
using EVA.SDK.Generator.V2.Inputs;

namespace EVA.SDK.Generator.V2.Commands.Generate;

public static class GenerationPipeline
{
  private static readonly INamedTransform[] _transforms =
  {
    new RemoveDeprecatedProperties(),
    new RemoveDeprecatedServices(),
    new RemoveEmptyTypes(),
    new RemoveErrors(),
    new RemoveEventExports(),
    new RemoveInheritance(),
    new RemoveNestedTypes(),
    new RemoveOptions(),
    new RemoveSharedRequestResponseTypes(),
    new RemoveUnusedGenericArguments()
  };

  private static ITransform _transform1 = new FixDependencies();
  private static ITransform _transform2 = new RemoveUnusedTypes();

  public static async Task Run(GenerateOptions opt, IOutput output)
  {
    // Some outputs require certain options
    output.FixOptions(opt);

    // Find all parts of the pipeline
    var input = InputFactory.GetInputFromString(opt.Input);
    var transforms = FindTransforms(opt).ToList();
    var filters = FindFilters(opt).ToList();

    // The input
    var model = await input.Read();

    // Run filters (once)
    foreach (var filter in filters) filter.Transform(model, opt);

    // Run transformations
    for (var i = 0; i < 10; i++)
    {
      Console.WriteLine($"\nRunning iteration {i}");
      var changes = ITransform.TransformResult.NoChanges;

      foreach (var transform in transforms)
      {
        var result = transform.Transform(model, opt);
        result |= _transform1.Transform(model, opt);
        result |= _transform2.Transform(model, opt);

        Console.WriteLine("[{0}]: {1}", transform.GetType().Name, result);

        // Clone model if needed
        if (result.HasFlag(ITransform.TransformResult.StructuralChanges))
        {
          model = JsonContext.Default.ApiDefinitionModel.Clone(model);
        }

        changes |= result;
      }

      if (changes == ITransform.TransformResult.NoChanges) break;
    }

    // Target directory handling
    EnsureEmptyOutputFolderExists(opt, output.OutputPattern);

    // Output
    await output.Write(model, opt.OutputDirectory);
  }

  /// <summary>
  /// This method will ensure the output path exists and is an empty directory.
  /// </summary>
  /// <exception cref="Exception"></exception>
  private static void EnsureEmptyOutputFolderExists(GenerateOptions opt, string? outputPattern)
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
        var allFiles = Directory.GetFiles(fullOutputPath, outputPattern, SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
          var relativeFile = Path.GetRelativePath(fullOutputPath, file);
          if (FileSystemName.MatchesSimpleExpression(outputPattern, relativeFile, true)) File.Delete(file);
        }
      }
    }
    else
    {
      if (File.Exists(fullOutputPath)) throw new Exception($"Cannot output to {fullOutputPath}, it is a file");
      if (Directory.Exists(fullOutputPath) && Directory.GetFileSystemEntries(fullOutputPath).Any()) throw new Exception($"Cannot output to {fullOutputPath}, it is not empty");
      Directory.CreateDirectory(fullOutputPath);
    }

    Console.WriteLine($"Outputting to: {fullOutputPath}");
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

  private static IEnumerable<ITransform> FindTransforms(GenerateOptions options)
  {
    var remove = (options.Remove ?? new List<string>()).Distinct().ToHashSet();

    foreach (var transform in _transforms)
    {
      if (remove.Contains(transform.Name)) yield return transform;
    }
  }
}