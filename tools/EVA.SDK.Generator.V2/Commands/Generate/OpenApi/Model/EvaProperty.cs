using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;

/// <summary>
/// When a change was announced, and in which API version it takes effect.
/// </summary>
internal sealed record EvaTiming(long Announced, long Active, string? Comment)
{
  internal static EvaTiming? From(JsonNode? node) => node == null
    ? null
    : new EvaTiming(node["announced"]!.GetValue<long>(), node["active"]!.GetValue<long>(), node["comment"]?.GetValue<string>());
}

/// <summary>
/// The property must be sent. When <see cref="Pending"/> is set, this is only announced and not enforced yet.
/// </summary>
internal sealed record EvaRequired(bool AllowEmpty, EvaTiming? Pending)
{
  internal bool IsEnforced => Pending == null;
}

/// <summary>
/// A range constraint. When <see cref="Pending"/> is set, this is only announced and not enforced yet.
/// </summary>
internal sealed record EvaRange(double Value, EvaTiming? Pending);

/// <summary>
/// The property holds the ID of an entity of type <see cref="Name"/>.
/// </summary>
/// <param name="Name">The entity type, eg Order.</param>
/// <param name="Lenient">Missing or invalid BackendIDs are allowed.</param>
/// <param name="BackendID">The BackendID can be passed instead when using EVA-IDs-Mode: ExternalIDs.</param>
/// <param name="SystemID">The ID can be resolved through EVA-IDs-BackendSystemID.</param>
/// <param name="ExternalID">The property holds an external ID of the entity instead of its EVA ID.</param>
internal sealed record EvaEntity(string Name, bool Lenient, bool BackendID, bool SystemID, bool ExternalID);

/// <summary>
/// A property of a type, read from its schema.
/// </summary>
internal sealed class EvaProperty
{
  private readonly EvaApi _api;

  internal EvaProperty(EvaApi api, string name, IOpenApiSchema schema)
  {
    _api = api;
    Name = name;
    Schema = schema;
  }

  internal string Name { get; }
  internal IOpenApiSchema Schema { get; }

  internal TypeRef Type => _api.ToTypeRef(Schema);

  internal string? Description => Schema is OpenApiSchemaReference r ? r.Reference.Description : Schema.Description;

  /// <summary>
  /// Leaving the property out means something else than sending null.
  /// </summary>
  internal bool Skippable => Schema.ExtensionValue<bool>(ExtensionNames.Skippable);

  internal EvaTiming? Deprecated => EvaTiming.From(Schema.ExtensionValue(ExtensionNames.Deprecated));

  internal EvaRequired? Required => Schema.ExtensionValue(ExtensionNames.Required) is { } required
    ? new EvaRequired(required["allowEmpty"]!.GetValue<bool>(), required["active"] == null ? null : EvaTiming.From(required))
    : null;

  internal EvaRange? Minimum => Range("minimum", ExtensionNames.Minimum);

  internal EvaRange? Maximum => Range("maximum", ExtensionNames.Maximum);

  internal int? MinLength => Schema is OpenApiSchemaReference r ? r.Reference.MinLength : Schema.MinLength;

  internal int? MaxLength => Schema is OpenApiSchemaReference r ? r.Reference.MaxLength : Schema.MaxLength;

  internal string? Pattern => Schema is OpenApiSchemaReference r ? r.Reference.Pattern : Schema.Pattern;

  internal EvaEntity? Entity => Schema.ExtensionValue(ExtensionNames.Entity) is { } entity
    ? new EvaEntity(
      entity["name"]!.GetValue<string>(),
      entity["lenient"]?.GetValue<bool>() ?? false,
      entity["backendID"]?.GetValue<bool>() ?? false,
      entity["systemID"]?.GetValue<bool>() ?? false,
      entity["externalID"]?.GetValue<bool>() ?? false)
    : null;

  /// <summary>
  /// The values a string property (or the strings of a list) can have.
  /// </summary>
  internal ImmutableArray<string> AllowedValues
  {
    get
    {
      if (Schema is not OpenApiSchema schema) return [];

      var target = schema.Enum != null ? schema : schema.Items as OpenApiSchema;
      // The null of a nullable string is not one of its values
      return target?.Enum?.OfType<JsonNode>().Where(v => !JsonNullSentinel.IsJsonNullSentinel(v)).Select(v => v.GetValue<string>()).ToImmutableArray() ?? [];
    }
  }

  private EvaRange? Range(string keyword, string pendingExtension)
  {
    var enforced = Schema is OpenApiSchemaReference r ? (keyword == "minimum" ? r.Reference.Minimum : r.Reference.Maximum) : (keyword == "minimum" ? Schema.Minimum : Schema.Maximum);
    if (enforced != null) return new EvaRange(double.Parse(enforced, CultureInfo.InvariantCulture), null);

    return Schema.ExtensionValue(pendingExtension) is { } pending ? new EvaRange(pending["value"]!.GetValue<double>(), EvaTiming.From(pending)) : null;
  }

  public override string ToString() => Name;
}
