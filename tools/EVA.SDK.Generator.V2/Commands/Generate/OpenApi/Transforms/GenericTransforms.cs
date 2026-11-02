using System.Text.Json.Nodes;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;

/// <summary>
/// Removes generic types. References to constructed generic types already refer to a component schema with the arguments filled in, so
/// these lose their type arguments and become regular types. Generic types that are no longer used are removed afterwards.
/// </summary>
internal class RemoveGenerics : IApiTransform
{
  public string ID => "remove-generics";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var changes = false;

    foreach (var schema in api.RootSchemas().SelectMany(Schemas.Descendants))
    {
      if (schema is OpenApiSchemaReference reference && reference.Extension(ExtensionNames.TypeArguments) != null)
      {
        reference.RemoveExtension(ExtensionNames.TypeArguments);
        changes = true;
      }
    }

    foreach (var type in api.AllTypes)
    {
      if (type.Template != null)
      {
        type.Schema.RemoveExtension(ExtensionNames.Template);
        changes = true;
      }

      if (type.TypeParameters.Any())
      {
        type.Schema.RemoveExtension(ExtensionNames.TypeParameters);
        changes = true;
      }
    }

    return changes;
  }
}

/// <summary>
/// Removes type parameters that a generic type doesn't use, and the corresponding arguments from all references to it.
/// </summary>
internal class RemoveUnusedTypeParameters : IApiTransform
{
  public string ID => "remove-unused-type-params";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var changes = false;

    foreach (var type in api.Types.Values.Where(t => t.TypeParameters.Any()).ToList())
    {
      var used = (type.Extends == null ? [] : type.Extends.Descendants())
        .Concat(type.Properties.Values.SelectMany(p => p.Type.Descendants()))
        .OfType<TypeParameterRef>()
        .Select(p => p.Name)
        .ToHashSet();

      var parameters = type.TypeParameters;
      var unusedIndices = Enumerable.Range(0, parameters.Length).Where(i => !used.Contains(parameters[i])).OrderDescending().ToList();
      if (unusedIndices.Count == 0) continue;

      changes = true;
      type.Schema.SetExtensionValue(ExtensionNames.TypeParameters, new JsonArray(parameters.Where((_, i) => !unusedIndices.Contains(i)).Select(p => (JsonNode)p).ToArray()));

      foreach (var schema in api.RootSchemas().SelectMany(Schemas.Descendants).ToList())
      {
        if (schema is not OpenApiSchemaReference reference || reference.Extension(ExtensionNames.TypeArguments) is not SchemaListExtension arguments) continue;
        if (api.ToTypeRef(reference) is not NamedRef named || named.TypeID != type.ID) continue;

        foreach (var index in unusedIndices) arguments.Schemas.RemoveAt(index);
      }

      api.Invalidate();
    }

    return changes;
  }
}
