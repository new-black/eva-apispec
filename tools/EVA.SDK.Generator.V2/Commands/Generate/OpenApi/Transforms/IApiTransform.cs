using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;

/// <summary>
/// Changes the OpenAPI document of the API. Returns whether anything changed.
/// </summary>
internal interface IApiTransform
{
  /// <summary>
  /// The ID used to request the transform, the same as the transform of the specification pipeline.
  /// </summary>
  string ID { get; }

  bool Transform(EvaApi api, GenerateOptions options, ILogger logger);
}

internal static class ApiTransformHelpers
{
  /// <summary>
  /// The schemas of all types, the schemas that can refer to types. Error arguments only keep the name of their type.
  /// </summary>
  internal static IEnumerable<IOpenApiSchema> RootSchemas(this EvaApi api) => api.AllTypes.Select(t => (IOpenApiSchema)t.Schema);

  /// <summary>
  /// Replaces schemas in all component schemas, see <see cref="Schemas.Replace"/>.
  /// </summary>
  internal static void ReplaceEverywhere(this EvaApi api, Func<IOpenApiSchema, IOpenApiSchema?> replace)
  {
    foreach (var type in api.AllTypes) Schemas.Replace(type.Schema, replace);
  }

  /// <summary>
  /// A reference to another component schema with the same keywords and extensions next to it.
  /// </summary>
  internal static OpenApiSchemaReference Retarget(this OpenApiSchemaReference reference, string componentName, OpenApiDocument document)
  {
    var result = (OpenApiSchemaReference)Schemas.Clone(reference, document);
    var retargeted = new OpenApiSchemaReference(componentName, document);
    retargeted.Reference.Description = result.Reference.Description;
    retargeted.Reference.Deprecated = result.Reference.Deprecated;
    retargeted.Reference.MinLength = result.Reference.MinLength;
    retargeted.Reference.MaxLength = result.Reference.MaxLength;
    retargeted.Reference.Pattern = result.Reference.Pattern;
    retargeted.Reference.Minimum = result.Reference.Minimum;
    retargeted.Reference.Maximum = result.Reference.Maximum;
    retargeted.Reference.Extensions = result.Reference.Extensions;
    return retargeted;
  }

  /// <summary>
  /// Removes the types that no service uses, directly or through other types.
  /// </summary>
  internal static bool RemoveUnusedTypes(this EvaApi api)
  {
    var used = new HashSet<string>();
    var toVisit = new Stack<string>(api.Services.SelectMany(s => new[] { s.RequestType.ComponentName, s.ResponseType.ComponentName }));

    while (toVisit.TryPop(out var componentName))
    {
      if (!used.Add(componentName) || api.TryGetTypeOfComponent(componentName) is not { } type) continue;
      foreach (var reference in Schemas.References(type.Schema)) toVisit.Push(reference.ComponentName());
    }

    var unused = api.AllTypes.Where(t => !used.Contains(t.ComponentName)).ToList();
    foreach (var type in unused) api.RemoveType(type);

    if (unused.Count > 0) api.Invalidate();
    return unused.Count > 0;
  }
}
