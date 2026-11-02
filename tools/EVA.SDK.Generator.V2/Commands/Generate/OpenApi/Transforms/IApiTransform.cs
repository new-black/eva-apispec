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
  /// All schemas of the document that can contain references: the component schemas and the error arguments.
  /// </summary>
  internal static IEnumerable<IOpenApiSchema> RootSchemas(this EvaApi api)
  {
    return api.AllTypes.Select(t => (IOpenApiSchema)t.Schema).Concat(api.Errors.SelectMany(e => e.Arguments).Select(a => a.Schema));
  }

  /// <summary>
  /// Replaces schemas in all component schemas and error arguments, see <see cref="Schemas.Replace"/>.
  /// </summary>
  internal static void ReplaceEverywhere(this EvaApi api, Func<IOpenApiSchema, IOpenApiSchema?> replace)
  {
    foreach (var type in api.AllTypes) Schemas.Replace(type.Schema, replace);

    foreach (var error in api.Errors)
    {
      for (var i = 0; i < error.Arguments.Count; i++)
      {
        error.Arguments[i] = error.Arguments[i] with { Schema = Schemas.Replace(error.Arguments[i].Schema, replace) };
      }
    }
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
  /// Removes the types that no service or error uses, directly or through other types.
  /// </summary>
  internal static bool RemoveUnusedTypes(this EvaApi api)
  {
    var used = new HashSet<string>();
    var toVisit = new Stack<string>(api.Services.SelectMany(s => new[] { s.RequestType.ComponentName, s.ResponseType.ComponentName })
      .Concat(api.Errors.SelectMany(e => e.Arguments).SelectMany(a => Schemas.References(a.Schema)).Select(r => r.ComponentName())));

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
