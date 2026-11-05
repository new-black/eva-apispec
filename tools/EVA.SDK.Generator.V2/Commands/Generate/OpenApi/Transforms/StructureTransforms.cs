using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;

/// <summary>
/// Flattens type hierarchies: every type gets the properties of its base types.
/// </summary>
internal class RemoveInheritance : IApiTransform
{
  public string ID => "remove-inheritance";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var changes = false;

    foreach (var type in api.AllTypes)
    {
      var schema = type.Schema;
      while (schema.AllOf is [var baseSchema])
      {
        if (baseSchema is not OpenApiSchemaReference baseReference)
        {
          logger.LogWarning("How can I merge {Base} into {Type}?", api.ToTypeRef(baseSchema).SpecName(), type.ID);
          break;
        }

        changes = true;
        var baseType = api.TypeOfComponent(baseReference.ComponentName());

        // A generic base type gets its arguments filled in
        var arguments = baseType.TypeParameters
          .Zip((baseReference.Extension(ExtensionNames.TypeArguments) as SchemaListExtension)?.Schemas ?? [])
          .ToDictionary(x => x.First, x => x.Second);

        IOpenApiSchema Copy(IOpenApiSchema s) => Substitute(Schemas.Clone(s, api.Document), arguments, api.Document);

        foreach (var (name, property) in baseType.Schema.Properties ?? new Dictionary<string, IOpenApiSchema>())
        {
          if (schema.Properties?.ContainsKey(name) ?? false) continue;

          schema.Properties ??= new SortedDictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
          schema.Properties[name] = Copy(property);
          if (baseType.Schema.Required?.Contains(name) ?? false) (schema.Required ??= new SortedSet<string>(StringComparer.Ordinal)).Add(name);
        }

        schema.AllOf = baseType.Schema.AllOf?.Select(Copy).ToList();
      }
    }

    return changes;
  }

  /// <summary>
  /// Replaces the type parameters in the schema by the given arguments. A nullable type parameter stays nullable.
  /// </summary>
  internal static IOpenApiSchema Substitute(IOpenApiSchema schema, IReadOnlyDictionary<string, IOpenApiSchema> arguments, OpenApiDocument document)
  {
    if (arguments.Count == 0) return schema;

    return Schemas.Replace(schema, s => s is OpenApiSchema && s.ExtensionValue<string>(ExtensionNames.TypeParameter) is { } name && arguments.TryGetValue(name, out var argument)
      ? Schemas.Clone(argument, document)
      : null);
  }
}

/// <summary>
/// Replaces options (properties that can hold any implementation of an interface) by the interface.
/// </summary>
internal class RemoveOptions : IApiTransform
{
  public string ID => "remove-options";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var changes = false;

    api.ReplaceEverywhere(s =>
    {
      if (s.Extension(ExtensionNames.Option) is not OptionExtension option) return null;

      changes = true;
      var shared = api.ToTypeRef(option.Shared);

      // An interface without any properties of its own is just an object
      if (shared is NamedRef named && api.GetType(named.TypeID) is { Properties.Count: 0, Extends: null })
      {
        return new OpenApiSchema { Type = shared.Nullable ? JsonSchemaType.Object | JsonSchemaType.Null : JsonSchemaType.Object };
      }

      return Schemas.Clone(option.Shared, api.Document);
    });

    return changes;
  }
}

/// <summary>
/// Removes types without properties. References to them refer to their base type instead, or to an object when they have none. Enums and
/// the request and response types of services are kept.
/// </summary>
internal class RemoveEmptyTypes : IApiTransform
{
  public string ID => "remove-empty-types";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var changes = false;

    while (true)
    {
      var kept = api.Services.SelectMany(s => new[] { s.RequestTypeID, s.ResponseTypeID }).ToHashSet();
      var type = api.AllTypes.FirstOrDefault(t => !t.Properties.Any() && !t.IsEnum && !kept.Contains(t.ID));
      if (type == null) break;

      changes = true;
      var redirect = type.Schema.AllOf is [var baseSchema] ? baseSchema : new OpenApiSchema { Type = JsonSchemaType.Object };

      api.ReplaceEverywhere(s => s is OpenApiSchemaReference r && r.ComponentName() == type.ComponentName ? Schemas.Clone(redirect, api.Document) : null);

      foreach (var other in api.AllTypes)
      {
        // Nested types of the removed type are no longer nested, constructed types of it are no longer generic
        if (other.Parent?.ComponentName == type.ComponentName) other.Schema.RemoveExtension(ExtensionNames.Parent);
        if (other.Template?.ComponentName == type.ComponentName)
        {
          other.Schema.RemoveExtension(ExtensionNames.Template);
          foreach (var reference in api.RootSchemas().SelectMany(Schemas.Descendants).OfType<OpenApiSchemaReference>().Where(r => r.ComponentName() == other.ComponentName))
          {
            reference.RemoveExtension(ExtensionNames.TypeArguments);
          }
        }

        // A base type that became an object is no base type
        if (other.Schema.AllOf is [OpenApiSchema { Type: JsonSchemaType.Object, Properties: null }]) other.Schema.AllOf = null;
      }

      api.RemoveType(type);
      api.Invalidate();
    }

    return changes;
  }
}

/// <summary>
/// Splits types that are used in requests and responses into a type for requests and a type for responses. The types for responses get
/// Response added to their name.
/// </summary>
internal class RemoveSharedRequestResponseTypes : IApiTransform
{
  public string ID => "remove-shared-req-res-types";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var shared = api.Types.Values.Where(t => !t.IsEnum && t.Usage is { request: true, response: true }).ToList();
    if (shared.Count == 0) return false;

    // The constructed types of a generic type are split with it
    var sharedIDs = shared.Select(t => t.ID).ToHashSet();
    shared.AddRange(api.AllTypes.Where(t => t.Template != null && sharedIDs.Contains(t.Template.ID)));

    var responseOnly = api.Types.Values.Where(t => t.Usage is { request: false, response: true }).Select(t => t.Schema).ToList();
    var renames = new Dictionary<string, string>();
    var copies = new List<OpenApiSchema>();

    foreach (var type in shared)
    {
      var id = type.ID;
      var name = type.Name;
      while (api.TryGetType(id) != null || renames.Values.Contains(Schemas.ComponentNameOf(id)))
      {
        id = AddResponse(id);
        name = AddResponse(name);
      }

      var copy = (OpenApiSchema)Schemas.Clone(type.Schema, api.Document);
      copy.SetExtensionValue(ExtensionNames.ID, id);
      copy.SetExtensionValue(ExtensionNames.Name, name);

      var componentName = Schemas.ComponentNameOf(id);
      renames[type.ComponentName] = componentName;
      api.AddType(componentName, copy);
      copies.Add(copy);
    }

    // The types used in responses refer to the copies
    IOpenApiSchema? Rename(IOpenApiSchema s) => s is OpenApiSchemaReference r && renames.TryGetValue(r.ComponentName(), out var renamed) ? r.Retarget(renamed, api.Document) : null;

    foreach (var schema in responseOnly.Concat(copies))
    {
      Schemas.Replace(schema, Rename);
      foreach (var name in new[] { ExtensionNames.Parent, ExtensionNames.Template })
      {
        if (schema.Extension(name) is SchemaExtension extension && Rename(extension.Schema) is { } renamed) extension.Schema = renamed;
      }
    }

    api.Invalidate();
    return true;
  }

  private static string AddResponse(string s)
  {
    var idx = s.IndexOf('`');
    if (idx == -1) return s + "Response";

    var idx2 = s.LastIndexOf('+');
    if (idx2 > idx) return s + "Response";

    return s[..idx] + "Response" + s[idx..];
  }
}
