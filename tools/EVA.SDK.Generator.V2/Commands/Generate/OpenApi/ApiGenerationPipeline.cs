using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs;
using EVA.SDK.Generator.V2.Exceptions;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi;

/// <summary>
/// An output generated from the OpenAPI document of an EVA environment.
/// </summary>
internal interface IApiOutput<T>
{
  string? OutputPattern { get; }
  bool GetForcedTransformations(T options, IApiTransform transform);
  Task Write(ApiOutputContext<T> ctx);
}

internal record ApiOutputContext<T>(EvaApi Api, T Options, OutputWriter Writer, ILogger Logger);

/// <summary>
/// Generates an output from the OpenAPI document of an EVA environment: download, filter, transform and write.
/// </summary>
internal static class ApiGenerationPipeline
{
  internal static IApiTransform[] AllTransforms =>
  [
    new RemoveGenerics(),
    new RemoveDeprecatedProperties(),
    new RemoveDeprecatedServices(),
    new RemoveEmptyTypes(),
    new RemoveErrors(),
    new RemoveInheritance(),
    new RemoveNestedTypes(),
    new RemoveOptions(),
    new RemoveSharedRequestResponseTypes(),
    new RemoveUnusedTypeParameters(),
    new RemoveEmptyBaseTypes()
  ];

  internal static async Task Run<T>(T opt, IApiOutput<T> output, ILogger logger) where T : GenerateOptions
  {
    if (opt.Api != "eva") throw new SdkException("Only the eva API can be generated from an OpenAPI document");

    var requested = opt.Transforms.ToHashSet();
    var transforms = AllTransforms.Where(t => requested.Contains(t.ID) || output.GetForcedTransformations(opt, t)).ToList();

    var api = await ApiInput.Read(opt, logger);

    // Filters run once
    var filters = new List<IApiTransform>();
    if (opt.FilterAssemblies is { Length: > 0 } || opt.MergeSmallAssemblies != null)
    {
      filters.Add(new FilterAssemblies(opt.FilterAssemblies, opt.MergeSmallAssemblies, opt.MergeSmallAssembliesLimit));
    }

    if (opt.FilterServices is { Length: > 0 }) filters.Add(new FilterServices(opt.FilterServices));

    foreach (var filter in filters)
    {
      filter.Transform(api, opt, logger);
      api.Invalidate();
    }

    api.RemoveUnusedTypes();

    // Transforms run until nothing changes
    for (var i = 0; i < 10; i++)
    {
      logger.LogDebug("Running iteration {Iteration}", i);
      var changes = false;

      foreach (var transform in transforms)
      {
        var result = transform.Transform(api, opt, logger);
        api.Invalidate();
        result |= api.RemoveUnusedTypes();

        logger.LogDebug("Transform {Transform} returned {Result}", transform.ID, result);
        changes |= result;
      }

      if (!changes) break;
    }

    // Orphaned types
    if (opt.OrphanedTypesAssembly != null)
    {
      var serviceAssemblies = api.Services.Select(s => s.Assembly).ToHashSet();
      foreach (var type in api.AllTypes)
      {
        if (!serviceAssemblies.Contains(type.Assembly)) type.Assembly = opt.OrphanedTypesAssembly;
      }

      foreach (var error in api.Errors)
      {
        if (!serviceAssemblies.Contains(error.Assembly)) error.Assembly = opt.OrphanedTypesAssembly;
      }

      api.Invalidate();
    }

    GenerationPipeline.EnsureEmptyOutputFolderExists(opt, output.OutputPattern, logger);

    var writer = new OutputWriter(opt.OutputDirectory, logger);
    await output.Write(new ApiOutputContext<T>(api, opt, writer, logger));

    writer.DeleteRemaining();
    writer.WriteReport(logger);
  }
}
