using System.Collections.Immutable;
using System.Text.Json.Nodes;
using EVA.SDK.Generator.V2.Exceptions;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;

/// <summary>
/// The EVA API described by an OpenAPI document. The services, types and errors are views on the document: transforms change the document
/// and call <see cref="Invalidate"/>, outputs read the views.
/// </summary>
internal sealed class EvaApi
{
  private const string ComponentPrefix = "#/components/schemas/";

  private List<EvaService>? _services;
  private Dictionary<string, EvaType>? _typesByComponentName;
  private SortedDictionary<string, EvaType>? _types;
  private Dictionary<string, EvaType>? _typesByID;
  private Dictionary<string, (bool request, bool response)>? _usage;
  private readonly Dictionary<string, ImmutableHashSet<int>> _usedTypeParameters = new();

  internal OpenApiDocument Document { get; }

  private EvaApi(OpenApiDocument document)
  {
    Document = document;
  }

  /// <summary>
  /// Reads the API from a document, turning the schemas in EVA extensions into schema objects.
  /// </summary>
  internal static EvaApi From(OpenApiDocument document)
  {
    foreach (var schema in document.Components?.Schemas?.Values ?? []) schema.ReadExtensionSchemas(document);

    var api = new EvaApi(document);
    var extensions = document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
    var errors = (extensions.Find(ExtensionNames.Errors) as JsonNodeExtension)?.Node.AsArray().Select(e => EvaError.Read(e!, api)).ToList() ?? [];
    extensions[ExtensionNames.Errors] = new ErrorsExtension(errors);

    return api;
  }

  internal int ApiVersion => (Document.Extensions![ExtensionNames.ApiVersion] as JsonNodeExtension)!.Node.GetValue<int>();

  /// <summary>
  /// The services, in the order of the document.
  /// </summary>
  internal IReadOnlyList<EvaService> Services => _services ??= Document.Paths
    .Select(p => new EvaService(this, p.Key, p.Value.Operations![HttpMethod.Post]))
    .ToList();

  /// <summary>
  /// The types outputs generate, by ID. Constructed generic types are left out while they refer to their generic type, references to them
  /// are references to the generic type with arguments.
  /// </summary>
  internal IReadOnlyDictionary<string, EvaType> Types => _types ??= new SortedDictionary<string, EvaType>(
    TypesByComponentName.Values.Where(t => t.Template == null).ToDictionary(t => t.ID),
    StringComparer.Ordinal);

  /// <summary>
  /// All types, the constructed generic types included.
  /// </summary>
  internal IReadOnlyCollection<EvaType> AllTypes => TypesByComponentName.Values;

  private Dictionary<string, EvaType> TypesByComponentName => _typesByComponentName ??= (Document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>())
    .ToDictionary(s => s.Key, s => new EvaType(this, s.Key, (OpenApiSchema)s.Value));

  internal List<EvaError> Errors => ((ErrorsExtension)Document.Extensions![ExtensionNames.Errors]).Errors;

  /// <summary>
  /// Drops the views, call this after changing the document.
  /// </summary>
  internal void Invalidate()
  {
    _services = null;
    _typesByComponentName = null;
    _types = null;
    _typesByID = null;
    _usage = null;
    _usedTypeParameters.Clear();
  }

  internal EvaType GetType(string id) => TryGetType(id) ?? throw new SdkException($"Unknown type {id}");

  internal EvaType? TryGetType(string id) => (_typesByID ??= AllTypes.ToDictionary(t => t.ID)).GetValueOrDefault(id);

  internal EvaType TypeOfComponent(string componentName) => TypesByComponentName.TryGetValue(componentName, out var type) ? type : throw new SdkException($"Unknown component schema {componentName}");

  internal EvaType? TryGetTypeOfComponent(string componentName) => TypesByComponentName.GetValueOrDefault(componentName);

  /// <summary>
  /// A reference to the component schema of a type.
  /// </summary>
  internal OpenApiSchemaReference ReferenceTo(EvaType type) => new(type.ComponentName, Document);

  internal IEnumerable<string> Assemblies => Services.Select(s => s.Assembly).Concat(Types.Values.Select(t => t.Assembly)).Distinct().Order(StringComparer.Ordinal);

  internal void RemoveService(EvaService service) => Document.Paths.Remove(service.Path);

  internal void RemoveType(EvaType type) => Document.Components!.Schemas!.Remove(type.ComponentName);

  internal void AddType(string componentName, OpenApiSchema schema) => Document.Components!.Schemas![componentName] = schema;

  /// <summary>
  /// Reads the type of a property, base type, generic argument or error argument from its schema.
  /// </summary>
  internal TypeRef ToTypeRef(IOpenApiSchema schema)
  {
    if (schema is OpenApiSchemaReference reference)
    {
      var target = TypeOfComponent(reference.ComponentName());
      var arguments = (reference.Extension(ExtensionNames.TypeArguments) as SchemaListExtension)?.Schemas.Select(ToTypeRef).ToImmutableArray() ?? [];

      // A constructed generic type is the generic type with its arguments
      var typeID = arguments.Any() && target.Template is { } template ? template.ID : target.ID;
      return new NamedRef(typeID, arguments, false);
    }

    var s = (OpenApiSchema)schema;

    if (s.Extension(ExtensionNames.Option) is OptionExtension option)
    {
      return new OptionRef(
        // An option that is not a type (eg an object after remove-empty-types) is no option
        s.AnyOf!.Where(o => !IsNull(o)).Select(o => ToTypeRef(o).WithNullable(true)).OfType<NamedRef>().ToImmutableArray(),
        ToTypeRef(option.Shared),
        s.AnyOf!.Any(IsNull));
    }

    if (s.AnyOf != null)
    {
      return ToTypeRef(s.AnyOf.Single(o => !IsNull(o))).WithNullable(true);
    }

    if (s.Not != null) return new PrimitiveRef(PrimitiveKind.Any, false);

    if (s.ExtensionValue<string>(ExtensionNames.TypeParameter) is { } typeParameter) return new TypeParameterRef(typeParameter, false);

    if (s.Type == null) return new PrimitiveRef(PrimitiveKind.Any, true);

    var nullable = s.Type.Value.HasFlag(JsonSchemaType.Null);
    var type = s.Type.Value & ~JsonSchemaType.Null;

    return type switch
    {
      JsonSchemaType.String => new PrimitiveRef(s.Format switch
      {
        null => PrimitiveKind.String,
        "eva-id" => PrimitiveKind.ID,
        "date-time" => PrimitiveKind.Date,
        "uuid" => PrimitiveKind.Guid,
        "time-span" => PrimitiveKind.Duration,
        "byte" => PrimitiveKind.Binary,
        var format => throw new SdkException($"Unknown string format {format}")
      }, nullable),
      JsonSchemaType.Integer => new PrimitiveRef(s.Format switch
      {
        "int16" => PrimitiveKind.Int16,
        "int32" => PrimitiveKind.Int32,
        "int64" => PrimitiveKind.Int64,
        var format => throw new SdkException($"Unknown integer format {format}")
      }, nullable),
      JsonSchemaType.Number => new PrimitiveRef(s.Format switch
      {
        "float" => PrimitiveKind.Float32,
        "double" => PrimitiveKind.Float64,
        "decimal" => PrimitiveKind.Decimal,
        var format => throw new SdkException($"Unknown number format {format}")
      }, nullable),
      JsonSchemaType.Boolean => new PrimitiveRef(PrimitiveKind.Bool, nullable),
      JsonSchemaType.Array => new ArrayRef(ToTypeRef(s.Items!), nullable),
      JsonSchemaType.Object when s.AdditionalProperties != null => new MapRef(
        s.Extension(ExtensionNames.MapKey) is SchemaExtension key ? ToTypeRef(key.Schema) : new PrimitiveRef(PrimitiveKind.String, true),
        ToTypeRef(s.AdditionalProperties),
        nullable),
      JsonSchemaType.Object => new PrimitiveRef(PrimitiveKind.Object, nullable),
      _ => throw new SdkException($"Unknown type {type}")
    };
  }

  private static bool IsNull(IOpenApiSchema schema) => schema is OpenApiSchema { Type: JsonSchemaType.Null };

  /// <summary>
  /// The indices of the type parameters of a generic type that its schema depends on. A type parameter that is only passed on as an argument
  /// that the other generic type doesn't use, doesn't count.
  /// </summary>
  internal ImmutableHashSet<int> UsedTypeParameters(EvaType type)
  {
    if (_usedTypeParameters.TryGetValue(type.ID, out var cached)) return cached;

    // Assume all are used while working this out, in case the type refers to itself
    var parameters = type.TypeParameters;
    _usedTypeParameters[type.ID] = Enumerable.Range(0, parameters.Length).ToImmutableHashSet();

    var used = new HashSet<string>();
    void Visit(TypeRef reference)
    {
      switch (reference)
      {
        case TypeParameterRef p:
          used.Add(p.Name);
          break;
        case NamedRef n when n.Arguments.Any():
          var usedArguments = UsedTypeParameters(GetType(n.TypeID));
          for (var i = 0; i < n.Arguments.Length; i++)
          {
            if (usedArguments.Contains(i)) Visit(n.Arguments[i]);
          }

          break;
        case ArrayRef a:
          Visit(a.Items);
          break;
        case MapRef m:
          Visit(m.Key);
          Visit(m.Value);
          break;
        case OptionRef o:
          Visit(o.Shared);
          foreach (var x in o.Options) Visit(x);
          break;
      }
    }

    if (type.Extends != null) Visit(type.Extends);
    foreach (var property in type.Properties.Values) Visit(property.Type);

    var result = Enumerable.Range(0, parameters.Length).Where(i => used.Contains(parameters[i])).ToImmutableHashSet();
    _usedTypeParameters[type.ID] = result;
    return result;
  }

  /// <summary>
  /// The types a reference depends on: the types it names, and the arguments of generic types that they use.
  /// </summary>
  internal IEnumerable<string> UsedTypes(TypeRef reference)
  {
    switch (reference)
    {
      case NamedRef n:
        yield return n.TypeID;
        var usedArguments = n.Arguments.Any() ? UsedTypeParameters(GetType(n.TypeID)) : [];
        for (var i = 0; i < n.Arguments.Length; i++)
        {
          if (!usedArguments.Contains(i)) continue;
          foreach (var x in UsedTypes(n.Arguments[i])) yield return x;
        }

        break;
      case ArrayRef a:
        foreach (var x in UsedTypes(a.Items)) yield return x;
        break;
      case MapRef m:
        foreach (var x in UsedTypes(m.Key).Concat(UsedTypes(m.Value))) yield return x;
        break;
      case OptionRef o:
        foreach (var x in UsedTypes(o.Shared).Concat(o.Options.SelectMany(UsedTypes))) yield return x;
        break;
    }
  }

  /// <summary>
  /// Whether a type is used in requests and in responses, through the types that use it.
  /// </summary>
  internal (bool request, bool response) UsageOf(string typeID)
  {
    if (_usage == null)
    {
      var request = Reachable(Services.Select(s => s.RequestTypeID));
      var response = Reachable(Services.Select(s => s.ResponseTypeID));
      _usage = Types.Keys.ToDictionary(id => id, id => (request.Contains(id), response.Contains(id)));
    }

    return _usage.GetValueOrDefault(typeID);
  }

  private HashSet<string> Reachable(IEnumerable<string> roots)
  {
    var result = new HashSet<string>();
    var toVisit = new Stack<string>(roots);

    while (toVisit.TryPop(out var id))
    {
      if (!result.Add(id) || TryGetType(id) is not { } type) continue;

      if (type.Extends != null) foreach (var x in UsedTypes(type.Extends)) toVisit.Push(x);
      foreach (var x in type.Properties.Values.SelectMany(p => UsedTypes(p.Type))) toVisit.Push(x);
    }

    return result;
  }

  /// <summary>
  /// The services, types and errors of each assembly.
  /// </summary>
  internal List<GroupedApi> GroupByAssembly()
  {
    var typesByAssembly = Types.GroupBy(t => t.Value.Assembly).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Key, x => x.Value));
    var errorsByAssembly = Errors.GroupBy(e => e.Assembly).ToDictionary(g => g.Key, g => g.ToList());
    var handled = new HashSet<string>();
    var result = new List<GroupedApi>();

    foreach (var g in Services.GroupBy(s => s.Assembly))
    {
      result.Add(new GroupedApi(g.Key, typesByAssembly.GetValueOrDefault(g.Key) ?? [], g.ToList(), errorsByAssembly.GetValueOrDefault(g.Key) ?? []));
      handled.Add(g.Key);
    }

    foreach (var (assembly, types) in typesByAssembly.Where(x => !handled.Contains(x.Key)))
    {
      result.Add(new GroupedApi(assembly, types, [], errorsByAssembly.GetValueOrDefault(assembly) ?? []));
    }

    return result;
  }

  internal record GroupedApi(string Assembly, Dictionary<string, EvaType> Types, List<EvaService> Services, List<EvaError> Errors);
}

internal static class TypeRefExtensions
{
  /// <summary>
  /// A short name of the type, eg string, int32, array or the type ID.
  /// </summary>
  internal static string SpecName(this TypeRef reference) => reference switch
  {
    PrimitiveRef p => p.Kind switch
    {
      PrimitiveKind.String => "string",
      PrimitiveKind.ID => "id",
      PrimitiveKind.Int16 => "int16",
      PrimitiveKind.Int32 => "int32",
      PrimitiveKind.Int64 => "int64",
      PrimitiveKind.Float32 => "float32",
      PrimitiveKind.Float64 => "float64",
      PrimitiveKind.Decimal => "float128",
      PrimitiveKind.Date => "date",
      PrimitiveKind.Guid => "guid",
      PrimitiveKind.Bool => "bool",
      PrimitiveKind.Duration => "duration",
      PrimitiveKind.Binary => "binary",
      PrimitiveKind.Object => "object",
      _ => "any"
    },
    ArrayRef => "array",
    MapRef => "map",
    NamedRef n => n.TypeID,
    TypeParameterRef t => t.Name,
    OptionRef => "option",
    _ => throw new SdkException($"Unknown reference {reference}")
  };
}
