using System.Text.Json.Nodes;
using EVA.SDK.Generator.V2.Exceptions;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;

/// <summary>
/// Helpers to read, walk, change and copy the schemas of the document, including the schemas held by EVA extensions.
/// </summary>
internal static partial class Schemas
{
  private const string ComponentPrefix = "#/components/schemas/";

  /// <summary>
  /// The name of the component schema of a type ID. Component names may only contain the characters a-z, A-Z, 0-9, '.', '-' and '_'.
  /// </summary>
  internal static string ComponentNameOf(string id) => InvalidComponentNameCharacters().Replace(id, "_").Trim('_');

  [System.Text.RegularExpressions.GeneratedRegex("[^a-zA-Z0-9-._]")]
  private static partial System.Text.RegularExpressions.Regex InvalidComponentNameCharacters();

  internal static void Serialize(IOpenApiSerializable element, IOpenApiWriter writer, OpenApiSpecVersion specVersion)
  {
    switch (specVersion)
    {
      case OpenApiSpecVersion.OpenApi2_0:
        element.SerializeAsV2(writer);
        break;
      case OpenApiSpecVersion.OpenApi3_0:
        element.SerializeAsV3(writer);
        break;
      case OpenApiSpecVersion.OpenApi3_1:
        element.SerializeAsV31(writer);
        break;
      default:
        element.SerializeAsV32(writer);
        break;
    }
  }

  /// <summary>
  /// The extensions of a schema, or those next to the reference when the schema is a reference.
  /// </summary>
  internal static IDictionary<string, IOpenApiExtension> Extensions(this IOpenApiSchema schema) => schema switch
  {
    OpenApiSchemaReference reference => reference.Reference.Extensions ??= new Dictionary<string, IOpenApiExtension>(),
    OpenApiSchema s => s.Extensions ??= new Dictionary<string, IOpenApiExtension>(),
    _ => throw new SdkException($"Unexpected schema type {schema.GetType().Name}")
  };

  internal static IOpenApiExtension? Extension(this IOpenApiSchema schema, string name) => schema switch
  {
    OpenApiSchemaReference reference => reference.Reference.Extensions.Find(name),
    _ => schema.Extensions.Find(name)
  };

  internal static IOpenApiExtension? Find(this IDictionary<string, IOpenApiExtension>? extensions, string name) => extensions != null && extensions.TryGetValue(name, out var extension) ? extension : null;

  internal static JsonNode? ExtensionValue(this IOpenApiSchema schema, string name) => (schema.Extension(name) as JsonNodeExtension)?.Node;

  internal static T? ExtensionValue<T>(this IOpenApiSchema schema, string name) => schema.ExtensionValue(name) is { } node ? node.GetValue<T>() : default;

  internal static void SetExtensionValue(this IOpenApiSchema schema, string name, JsonNode value) => schema.Extensions()[name] = new JsonNodeExtension(value);

  internal static void RemoveExtension(this IOpenApiSchema schema, string name) => schema.Extensions().Remove(name);

  /// <summary>
  /// The name of the component schema a reference refers to.
  /// </summary>
  internal static string ComponentName(this OpenApiSchemaReference reference) => reference.Reference.Id!;

  /// <summary>
  /// Turns the schemas the EVA extensions hold into schema objects, they are read as plain JSON.
  /// </summary>
  internal static void ReadExtensionSchemas(this IOpenApiSchema schema, OpenApiDocument document)
  {
    var extensions = schema is OpenApiSchemaReference r ? r.Reference.Extensions : schema.Extensions;
    if (extensions != null)
    {
      foreach (var (name, extension) in extensions.ToList())
      {
        if (extension is not JsonNodeExtension json) continue;

        extensions[name] = name switch
        {
          ExtensionNames.TypeArguments => new SchemaListExtension(json.Node.AsArray().Select(n => Read(n!, document)).ToList()),
          ExtensionNames.Option => new OptionExtension(Read(json.Node["shared"]!, document)),
          ExtensionNames.MapKey or ExtensionNames.Parent or ExtensionNames.Template => new SchemaExtension(Read(json.Node, document)),
          _ => extension
        };
      }
    }

    foreach (var child in Children(schema)) child.ReadExtensionSchemas(document);
  }

  /// <summary>
  /// Reads a schema from the JSON of an extension. These are always references to types, so only the keywords that express those occur.
  /// </summary>
  internal static IOpenApiSchema Read(JsonNode node, OpenApiDocument document)
  {
    var json = node.AsObject();

    if (json["$ref"] is { } reference)
    {
      var path = reference.GetValue<string>();
      if (!path.StartsWith(ComponentPrefix)) throw new SdkException($"Unexpected reference {path}");

      var result = new OpenApiSchemaReference(path[ComponentPrefix.Length..], document);
      foreach (var (key, value) in json)
      {
        switch (key)
        {
          case "$ref":
            break;
          case ExtensionNames.TypeArguments:
            result.Extensions()[key] = new SchemaListExtension(value!.AsArray().Select(n => Read(n!, document)).ToList());
            break;
          default:
            throw new SdkException($"Unexpected keyword {key} next to a reference");
        }
      }

      return result;
    }

    var schema = new OpenApiSchema();
    foreach (var (key, value) in json)
    {
      switch (key)
      {
        case "type":
          schema.Type = value is JsonArray types
            ? types.Select(t => ParseType(t!.GetValue<string>())).Aggregate((a, b) => a | b)
            : ParseType(value!.GetValue<string>());
          break;
        case "format":
          schema.Format = value!.GetValue<string>();
          break;
        case "items":
          schema.Items = Read(value!, document);
          break;
        case "additionalProperties":
          schema.AdditionalProperties = Read(value!, document);
          break;
        case "anyOf":
          schema.AnyOf = value!.AsArray().Select(n => Read(n!, document)).ToList();
          break;
        case "not":
          schema.Not = Read(value!, document);
          break;
        case "enum":
          schema.Enum = value!.AsArray().Select(v => v?.DeepClone()!).ToList();
          break;
        case ExtensionNames.TypeParameter:
          schema.Extensions()[key] = new JsonNodeExtension(value!.DeepClone());
          break;
        case ExtensionNames.MapKey:
          schema.Extensions()[key] = new SchemaExtension(Read(value!, document));
          break;
        case ExtensionNames.Option:
          schema.Extensions()[key] = new OptionExtension(Read(value!["shared"]!, document));
          break;
        default:
          throw new SdkException($"Unexpected keyword {key} in a type reference");
      }
    }

    return schema;
  }

  private static JsonSchemaType ParseType(string type) => type switch
  {
    "null" => JsonSchemaType.Null,
    "boolean" => JsonSchemaType.Boolean,
    "integer" => JsonSchemaType.Integer,
    "number" => JsonSchemaType.Number,
    "string" => JsonSchemaType.String,
    "object" => JsonSchemaType.Object,
    "array" => JsonSchemaType.Array,
    _ => throw new SdkException($"Unexpected type {type}")
  };

  /// <summary>
  /// The schemas directly inside a schema: properties, items, base types, alternatives, map values and the schemas held by EVA extensions,
  /// except the parent of a nested type and the template of a constructed generic type. References are not followed.
  /// </summary>
  internal static IEnumerable<IOpenApiSchema> Children(IOpenApiSchema schema)
  {
    if (schema is OpenApiSchema s)
    {
      if (s.Properties != null) foreach (var x in s.Properties.Values) yield return x;
      if (s.Items != null) yield return s.Items;
      if (s.AllOf != null) foreach (var x in s.AllOf) yield return x;
      if (s.AnyOf != null) foreach (var x in s.AnyOf) yield return x;
      if (s.AdditionalProperties != null) yield return s.AdditionalProperties;
      if (s.Not != null) yield return s.Not;
    }

    foreach (var x in ExtensionSchemas(schema)) yield return x;
  }

  private static IEnumerable<IOpenApiSchema> ExtensionSchemas(IOpenApiSchema schema)
  {
    var extensions = schema is OpenApiSchemaReference r ? r.Reference.Extensions : schema.Extensions;
    if (extensions == null) yield break;

    foreach (var (name, extension) in extensions)
    {
      switch (extension)
      {
        case SchemaListExtension list:
          foreach (var x in list.Schemas) yield return x;
          break;
        case OptionExtension option:
          yield return option.Shared;
          break;
        case SchemaExtension single when name is not (ExtensionNames.Parent or ExtensionNames.Template):
          yield return single.Schema;
          break;
      }
    }
  }

  /// <summary>
  /// The schema and all schemas inside it, see <see cref="Children"/>.
  /// </summary>
  internal static IEnumerable<IOpenApiSchema> Descendants(IOpenApiSchema schema)
  {
    yield return schema;
    foreach (var x in Children(schema).SelectMany(Descendants)) yield return x;
  }

  /// <summary>
  /// The references in the schema, including those to the parent of a nested type and the template of a constructed generic type.
  /// </summary>
  internal static IEnumerable<OpenApiSchemaReference> References(IOpenApiSchema schema)
  {
    foreach (var x in Descendants(schema))
    {
      if (x is OpenApiSchemaReference reference) yield return reference;
      if (x.Extension(ExtensionNames.Parent) is SchemaExtension { Schema: OpenApiSchemaReference parent }) yield return parent;
      if (x.Extension(ExtensionNames.Template) is SchemaExtension { Schema: OpenApiSchemaReference template }) yield return template;
    }
  }

  /// <summary>
  /// Replaces schemas inside the schema. <paramref name="replace"/> returns the replacement of a schema, or null to keep it and look
  /// inside it. Returns the schema itself, or its replacement.
  /// </summary>
  internal static IOpenApiSchema Replace(IOpenApiSchema schema, Func<IOpenApiSchema, IOpenApiSchema?> replace)
  {
    if (replace(schema) is { } replacement) return CopyPropertyKeywords(schema, replacement);

    IOpenApiSchema ReplaceChild(IOpenApiSchema child) => Replace(child, replace);
    void ReplaceAll(IList<IOpenApiSchema>? list)
    {
      if (list == null) return;
      for (var i = 0; i < list.Count; i++) list[i] = ReplaceChild(list[i]);
    }

    if (schema is OpenApiSchema s)
    {
      if (s.Properties != null)
      {
        foreach (var key in s.Properties.Keys.ToList()) s.Properties[key] = ReplaceChild(s.Properties[key]);
      }

      if (s.Items != null) s.Items = ReplaceChild(s.Items);
      ReplaceAll(s.AllOf);
      ReplaceAll(s.AnyOf);
      if (s.AdditionalProperties != null) s.AdditionalProperties = ReplaceChild(s.AdditionalProperties);
      if (s.Not != null) s.Not = ReplaceChild(s.Not);
    }

    var extensions = schema is OpenApiSchemaReference r ? r.Reference.Extensions : schema.Extensions;
    foreach (var (name, extension) in extensions?.ToList() ?? [])
    {
      switch (extension)
      {
        case SchemaListExtension list:
          ReplaceAll(list.Schemas);
          break;
        case OptionExtension option:
          option.Shared = ReplaceChild(option.Shared);
          break;
        case SchemaExtension single when name is not (ExtensionNames.Parent or ExtensionNames.Template):
          single.Schema = ReplaceChild(single.Schema);
          break;
      }
    }

    return schema;
  }

  private static readonly string[] _propertyExtensions =
  [
    ExtensionNames.Skippable, ExtensionNames.Deprecated, ExtensionNames.Required, ExtensionNames.Minimum, ExtensionNames.Maximum, ExtensionNames.Entity
  ];

  /// <summary>
  /// The schema of a property also describes the property itself (its description, constraints and EVA property extensions). When the
  /// schema is replaced, the replacement describes the property the same way.
  /// </summary>
  private static IOpenApiSchema CopyPropertyKeywords(IOpenApiSchema original, IOpenApiSchema replacement)
  {
    var description = original is OpenApiSchemaReference r ? r.Reference.Description : original.Description;
    var deprecated = original is OpenApiSchemaReference r2 ? r2.Reference.Deprecated ?? false : original.Deprecated;
    var (minLength, maxLength, pattern, minimum, maximum) = original is OpenApiSchemaReference r3
      ? (r3.Reference.MinLength, r3.Reference.MaxLength, r3.Reference.Pattern, r3.Reference.Minimum, r3.Reference.Maximum)
      : (original.MinLength, original.MaxLength, original.Pattern, original.Minimum, original.Maximum);

    replacement.Update(
      s =>
      {
        s.Description ??= description;
        s.Deprecated |= deprecated;
        s.MinLength ??= minLength;
        s.MaxLength ??= maxLength;
        s.Pattern ??= pattern;
        s.Minimum ??= minimum;
        s.Maximum ??= maximum;
      },
      reference =>
      {
        reference.Description ??= description;
        if (deprecated) reference.Deprecated = true;
        reference.MinLength ??= minLength;
        reference.MaxLength ??= maxLength;
        reference.Pattern ??= pattern;
        reference.Minimum ??= minimum;
        reference.Maximum ??= maximum;
      });

    foreach (var name in _propertyExtensions)
    {
      if (original.Extension(name) is JsonNodeExtension extension && replacement.Extension(name) == null)
      {
        replacement.Extensions()[name] = new JsonNodeExtension(extension.Node.DeepClone());
      }
    }

    return replacement;
  }

  /// <summary>
  /// Changes the schema, or the keywords next to the reference when the schema is a reference.
  /// </summary>
  internal static void Update(this IOpenApiSchema schema, Action<OpenApiSchema> updateSchema, Action<JsonSchemaReference> updateReference)
  {
    switch (schema)
    {
      case OpenApiSchemaReference reference:
        updateReference(reference.Reference);
        break;
      case OpenApiSchema s:
        updateSchema(s);
        break;
    }
  }

  /// <summary>
  /// A deep copy of the schema. References are copied, not what they refer to.
  /// </summary>
  internal static IOpenApiSchema Clone(IOpenApiSchema schema, OpenApiDocument document)
  {
    if (schema is OpenApiSchemaReference reference)
    {
      var copy = new OpenApiSchemaReference(reference.ComponentName(), document);
      copy.Reference.Description = reference.Reference.Description;
      copy.Reference.Deprecated = reference.Reference.Deprecated;
      copy.Reference.MinLength = reference.Reference.MinLength;
      copy.Reference.MaxLength = reference.Reference.MaxLength;
      copy.Reference.Pattern = reference.Reference.Pattern;
      copy.Reference.Minimum = reference.Reference.Minimum;
      copy.Reference.Maximum = reference.Reference.Maximum;
      copy.Reference.Extensions = CloneExtensions(reference.Reference.Extensions, document);
      return copy;
    }

    var s = (OpenApiSchema)schema;
    return new OpenApiSchema
    {
      Type = s.Type,
      Format = s.Format,
      Description = s.Description,
      Deprecated = s.Deprecated,
      Enum = s.Enum?.Select(v => v?.DeepClone()!).ToList(),
      Items = s.Items == null ? null : Clone(s.Items, document),
      AdditionalProperties = s.AdditionalProperties == null ? null : Clone(s.AdditionalProperties, document),
      AllOf = s.AllOf?.Select(x => Clone(x, document)).ToList(),
      AnyOf = s.AnyOf?.Select(x => Clone(x, document)).ToList(),
      Not = s.Not == null ? null : Clone(s.Not, document),
      Properties = s.Properties == null ? null : new SortedDictionary<string, IOpenApiSchema>(s.Properties.ToDictionary(p => p.Key, p => Clone(p.Value, document)), StringComparer.Ordinal),
      Required = s.Required == null ? null : new SortedSet<string>(s.Required, StringComparer.Ordinal),
      MinLength = s.MinLength,
      MaxLength = s.MaxLength,
      Pattern = s.Pattern,
      Minimum = s.Minimum,
      Maximum = s.Maximum,
      Extensions = CloneExtensions(s.Extensions, document)
    };
  }

  private static Dictionary<string, IOpenApiExtension>? CloneExtensions(IDictionary<string, IOpenApiExtension>? extensions, OpenApiDocument document)
  {
    return extensions?.ToDictionary(e => e.Key, IOpenApiExtension (e) => e.Value switch
    {
      JsonNodeExtension json => new JsonNodeExtension(json.Node.DeepClone()),
      SchemaExtension single => new SchemaExtension(Clone(single.Schema, document)),
      SchemaListExtension list => new SchemaListExtension(list.Schemas.Select(x => Clone(x, document)).ToList()),
      OptionExtension option => new OptionExtension(Clone(option.Shared, document)),
      _ => throw new SdkException($"Cannot copy extension {e.Key}")
    });
  }
}
