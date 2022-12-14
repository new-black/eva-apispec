using System.Text.Json;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator;

public static class GenerationRunner
{
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
        Console.WriteLine("[{0}]: {1}", transform.GetType().Name, result);
        if (result == ITransform.TransformResult.Changes)
          model = JsonSerializer.Deserialize(
            JsonSerializer.Serialize(model, NonIndentedSerializationHelper.Default.ApiDefinitionModel),
            NonIndentedSerializationHelper.Default.ApiDefinitionModel
          )!;
        changes |= result;
      }

      if (changes == ITransform.TransformResult.NoChanges) break;
    }

    // Target directory handling
    EnsureEmptyOutputFolderExists(opt);

    // Output
    await output.Write(model, opt.OutputDirectory);
  }

  /// <summary>
  /// This method will ensure the output path exists and is an empty directory.
  /// </summary>
  /// <exception cref="Exception"></exception>
  private static void EnsureEmptyOutputFolderExists(GenerateOptions opt)
  {
    var fullOutputPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), opt.OutputDirectory));
    opt.OutputDirectory = fullOutputPath;
    if (opt.Overwrite)
    {
      if (File.Exists(fullOutputPath)) File.Delete(fullOutputPath);
      if (Directory.Exists(fullOutputPath)) Directory.Delete(fullOutputPath, true);
      Directory.CreateDirectory(fullOutputPath);
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
    var remove = options.Remove.Distinct().ToList();

    if (remove.Remove("options")) yield return new RemoveOptions();
    if (remove.Remove("deprecated-services")) yield return new RemoveDeprecatedServices();
    if (remove.Remove("deprecated-properties")) yield return new RemoveDeprecatedProperties();

    yield return new FixDependencies();
    yield return new RemoveUnusedTypes();

    if (remove.Remove("inheritance")) yield return new RemoveInheritance();
    if (remove.Remove("generics")) yield return new FlattenGenerics();
    if (remove.Remove("unused-type-params")) yield return new RemoveUnusedGenericArguments();
    if (remove.Remove("empty-types")) yield return new RemoveEmptyTypes();
    if (remove.Remove("nested-types")) yield return new RemoveNestedTypes();
    if (remove.Remove("event-exports")) yield return new RemoveEventExports();
    if (remove.Remove("errors")) yield return new RemoveErrors();
    if (remove.Remove("shared-req-res-types")) yield return new RemoveSharedRequestResponseTypes();

    yield return new FixDependencies();
    yield return new RemoveUnusedTypes();
  }
}