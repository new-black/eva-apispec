using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;

/// <summary>
/// A value of an enum, <see cref="Value"/> is its total numeric value.
/// </summary>
internal sealed record EvaEnumValue(string Name, long Value, string? Description);

/// <summary>
/// A type of the API, read from its component schema.
/// </summary>
internal sealed class EvaType
{
  private readonly EvaApi _api;
  private IReadOnlyDictionary<string, EvaProperty>? _properties;

  internal EvaType(EvaApi api, string componentName, OpenApiSchema schema)
  {
    _api = api;
    ComponentName = componentName;
    Schema = schema;
  }

  internal string ComponentName { get; }
  internal OpenApiSchema Schema { get; }

  /// <summary>
  /// The type ID, eg EVA.Core.Orders.OrderDto+Line.
  /// </summary>
  internal string ID => Schema.ExtensionValue<string>(ExtensionNames.ID)!;

  /// <summary>
  /// The type name, eg Line or PagedResult`1.
  /// </summary>
  internal string Name
  {
    get => Schema.ExtensionValue<string>(ExtensionNames.Name)!;
    set => Schema.SetExtensionValue(ExtensionNames.Name, value);
  }

  internal string Assembly
  {
    get => Schema.ExtensionValue<string>(ExtensionNames.Assembly)!;
    set => Schema.SetExtensionValue(ExtensionNames.Assembly, value);
  }

  internal string? Description => Schema.Description;

  /// <summary>
  /// The type this type is nested in.
  /// </summary>
  internal EvaType? Parent => Schema.Extension(ExtensionNames.Parent) is SchemaExtension { Schema: OpenApiSchemaReference parent } ? _api.TypeOfComponent(parent.ComponentName()) : null;

  /// <summary>
  /// The type parameters of a generic type, eg TModel.
  /// </summary>
  internal ImmutableArray<string> TypeParameters => Schema.ExtensionValue(ExtensionNames.TypeParameters) is JsonArray parameters
    ? parameters.Select(p => p!.GetValue<string>()).ToImmutableArray()
    : [];

  /// <summary>
  /// The generic type this is a constructed type of, with its arguments filled in.
  /// </summary>
  internal EvaType? Template => Schema.Extension(ExtensionNames.Template) is SchemaExtension { Schema: OpenApiSchemaReference template } ? _api.TypeOfComponent(template.ComponentName()) : null;

  internal bool IsEnum => Schema.Type == JsonSchemaType.Integer;

  internal bool IsFlags => Schema.Extension(ExtensionNames.FlagValues) != null;

  /// <summary>
  /// The values of an enum, by numeric value.
  /// </summary>
  internal IReadOnlyList<EvaEnumValue> EnumValues
  {
    get
    {
      if (!IsEnum) return [];

      var values = (IsFlags ? Schema.ExtensionValue(ExtensionNames.FlagValues)!.AsArray().Select(v => v!.GetValue<long>()) : Schema.Enum!.Select(v => v.GetValue<long>())).ToList();
      var names = Schema.ExtensionValue(ExtensionNames.EnumNames)!.AsArray().Select(n => n!.GetValue<string>()).ToList();
      var descriptions = Schema.ExtensionValue(ExtensionNames.EnumDescriptions)?.AsArray().Select(d => d!.GetValue<string>()).ToList();

      return names.Select((name, i) => new EvaEnumValue(name, values[i], descriptions?[i] is { Length: > 0 } d ? d : null)).ToList();
    }
  }

  /// <summary>
  /// The values of an enum by numeric value. Values with the same numeric value are listed by name, except that the flags a value combines
  /// come before it.
  /// </summary>
  internal IReadOnlyList<EvaEnumValue> EnumValuesByValue
  {
    get
    {
      var values = EnumValues;
      var byName = values.ToDictionary(v => v.Name);
      var result = new List<EvaEnumValue>();

      void Add(EvaEnumValue value)
      {
        if (result.Contains(value)) return;
        if (IsFlags)
        {
          foreach (var flag in FlagComposition(value).flags) Add(byName[flag]);
        }

        result.Add(value);
      }

      foreach (var value in values.OrderBy(v => v.Name, StringComparer.Ordinal)) Add(value);
      return result.OrderBy(v => v.Value).ToList();
    }
  }

  /// <summary>
  /// Writes a flag as the other flags it combines plus what remains: going through the values from high to low, a flag combines every
  /// other non-zero value that is fully contained in what remains.
  /// </summary>
  internal (long remainder, ImmutableArray<string> flags) FlagComposition(EvaEnumValue value)
  {
    var remainder = value.Value;
    var flags = new List<string>();

    foreach (var other in EnumValues.OrderByDescending(v => v.Value))
    {
      if (other.Name == value.Name || other.Value == value.Value || other.Value == 0) continue;
      if ((remainder & other.Value) != other.Value) continue;

      remainder -= other.Value;
      flags.Add(other.Name);
    }

    return (remainder, [..flags]);
  }

  /// <summary>
  /// The base type.
  /// </summary>
  internal TypeRef? Extends => Schema.AllOf is [var baseType] ? _api.ToTypeRef(baseType) : null;

  /// <summary>
  /// The properties declared on this type, by name.
  /// </summary>
  internal IReadOnlyDictionary<string, EvaProperty> Properties => _properties ??= new SortedDictionary<string, EvaProperty>(
    (Schema.Properties ?? new Dictionary<string, IOpenApiSchema>()).ToDictionary(p => p.Key, p => new EvaProperty(_api, p.Key, p.Value)),
    StringComparer.Ordinal);

  /// <summary>
  /// Whether the type is used in requests and in responses.
  /// </summary>
  internal (bool request, bool response) Usage => _api.UsageOf(ID);

  public override string ToString() => ID;
}
