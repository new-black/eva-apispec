using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Outputs.dotnet;

internal class DotNetOutput : IApiOutput<DotNetOptions>
{
  private const string DayOfWeek = "EVA.Core.DayOfWeek";
  private const string IProductSearchItem = "EVA.Core.Search.IProductSearchItem";

  private class GenerationState
  {
    public Dictionary<string, List<TypeRef>> AllOptionsImplementations;
    public EvaApi Api;

    private Dictionary<string, (string local, string full)> typeNameCache = [];
    private HashSet<(string ns, string local)> allocatedFullTypeNames = [];


    public (string localName, string fullName) AllocateName(string id)
    {
      if (typeNameCache.TryGetValue(id, out var x)) return (x.local, x.full);

      // Build the local name for the NS
      var spec = Api.GetType(id);
      var localName = spec.Name.Split('`')[0];

      if (spec.Parent != null)
      {
        var (parentLocal, _) = AllocateName(spec.Parent.ID);
        localName = $"{parentLocal}.{localName}";
      }

      var ns = FixNamespace(spec.Assembly);
      var originalLocalName = localName;

      var i = 2;
      while (allocatedFullTypeNames.Contains((ns, localName)))
      {
        localName = $"{originalLocalName}{i++}";
      }

      var fullName = $"{ns}.{localName}";

      typeNameCache[id] = (localName, fullName);
      allocatedFullTypeNames.Add((ns, localName));
      return (localName, fullName);
    }
  }

  [Flags]
  private enum TypeContext
  {
    None = 0,
    Request = 1,
    Response = 2
  }

  public string? OutputPattern => null;

  public bool GetForcedTransformations(DotNetOptions options, IApiTransform x) =>
    options.GenerateDynamicData
      ? x is RemoveInheritance
      : x is RemoveInheritance or RemoveOptions;

  private static void WriteErrors(EvaErrorExtensions.PrefixGroupedErrors errors, IndentedStringBuilder o, string name, EvaApi api)
  {
    if (!errors.Errors.Any() && !errors.SubErrors.Any()) return;

    o.WriteLine($"public static class {name}");
    using (o.BracedIndentation)
    {
      foreach (var e in errors.Errors)
      {
        o.WriteLines(e.Error.MessageWithEnhancedArguments(api), "/// ");
        o.WriteLine($"public const string {e.Name} = \"{e.Error.Name}\";");
      }

      foreach (var (prefix, suberrors) in errors.SubErrors)
      {
        WriteErrors(suberrors, o, prefix, api);
      }
    }
  }

  /// <summary>
  /// All type references in the types, like the old EnumerateAllTypeReferences.
  /// </summary>
  private static IEnumerable<TypeRef> AllTypeReferences(EvaApi api) => api.Types.Values.SelectMany(t =>
    (t.Extends == null ? [] : t.Extends.Descendants()).Concat(t.Properties.Values.SelectMany(p => p.Type.Descendants())));

  public async Task Write(ApiOutputContext<DotNetOptions> ctx)
  {
    var api = ctx.Api;
    var allResponseTypes = api.Services.Select(s => s.ResponseTypeID).ToHashSet();

    // Some preprocessing
    var state = new GenerationState
    {
      Api = api,
      AllOptionsImplementations = AllTypeReferences(api)
        .OfType<OptionRef>()
        .SelectMany(x => x.Options.Select(a => (x.Shared, Name: a.TypeID)))
        .GroupBy(x => x.Name)
        .ToDictionary(x => x.Key, x => x.Select(y => y.Shared).ToList())
    };

    if (ctx.Options.AddEvaClient)
    {
      var content = ManifestResourceHelpers.GetResource("dotnet.Resources.EVAClient.cs")!
        .Replace("<<API_VERSION>>", api.ApiVersion.ToString());
      await ctx.Writer.WriteFileAsync("EVAClient.cs", content);

      var serializationResource = ctx.Options.UseSystemTextJson
        ? "dotnet.Resources.EVAClient.SystemTextJson.cs"
        : "dotnet.Resources.EVAClient.NewtonsoftJson.cs";
      var serializationFile = ctx.Options.UseSystemTextJson
        ? "EVAClient.SystemTextJson.cs"
        : "EVAClient.NewtonsoftJson.cs";
      await ctx.Writer.WriteFileAsync(
        serializationFile,
        ManifestResourceHelpers.GetResource(serializationResource)!
      );
    }

    await ctx.Writer.WriteFileAsync("GlobalUsings.cs", ManifestResourceHelpers.GetResource("dotnet.Resources.GlobalUsings.cs")!);

    var handledTypes = new HashSet<string>();

    // First we write out the requests
    foreach(var service in api.Services)
    {
      var requestType = api.GetType(service.RequestTypeID);
      var actualNamespace = FixNamespace(requestType.Assembly);
      var sb = new IndentedStringBuilder(2);

      sb.WriteLine("#pragma warning disable CS8618");
      sb.WriteLine(JsonTypesUsing(ctx.Options));
      sb.WriteLine();

      sb.WriteLine($"namespace {actualNamespace};");
      sb.WriteLine();

      var fullRequestTypeName = WriteRequestType(service.RequestTypeID, requestType, sb, service, ctx, allResponseTypes, state);
      handledTypes.Add(service.RequestTypeID);

      await ctx.Writer.WriteFileAsync($"requests/{fullRequestTypeName}.cs", sb.ToString());
    }

    // Next we write out the responses
    foreach(var service in api.Services)
    {
      if (!handledTypes.Add(service.ResponseTypeID)) continue;

      var responseType = api.GetType(service.ResponseTypeID);
      var actualNamespace = FixNamespace(responseType.Assembly);
      var sb = new IndentedStringBuilder(2);

      sb.WriteLine("#pragma warning disable CS8618");
      sb.WriteLine(JsonTypesUsing(ctx.Options));
      sb.WriteLine();

      sb.WriteLine($"namespace {actualNamespace};");
      sb.WriteLine();

      var fullResponseTypeName = WriteResponseType(service.ResponseTypeID, responseType, sb, ctx, allResponseTypes, state);

      await ctx.Writer.WriteFileAsync($"responses/{fullResponseTypeName}.cs", sb.ToString());
    }

    // As the following step, we write out all the remaining types
    foreach (var type in api.Types.Where(type => type.Value.Parent == null))
    {
      if(!handledTypes.Add(type.Key)) continue;

      var actualNamespace = FixNamespace(type.Value.Assembly);
      var sb = new IndentedStringBuilder(2);

      sb.WriteLine("#pragma warning disable CS8618");
      sb.WriteLine(JsonTypesUsing(ctx.Options));

      sb.WriteLine();
      sb.WriteLine($"namespace {actualNamespace};");
      sb.WriteLine();

      var fullTypeName = WriteType(type.Key, type.Value, sb, ctx, allResponseTypes, state);
      if (fullTypeName == null) continue;

      await ctx.Writer.WriteFileAsync($"types/{fullTypeName}.cs", sb.ToString());
    }

    // We also need the list of possible errors
    {
      foreach (var group in api.Errors.GroupBy(x => x.Assembly))
      {
        var sb = new IndentedStringBuilder(2);
        var ns = FixNamespace(group.Key);
        sb.WriteLine($"namespace {ns};");
        sb.WriteLine();
        WriteErrors(group.GroupByPrefix(), sb, "Errors", api);
        await ctx.Writer.WriteFileAsync($"errors/{ns}.cs", sb.ToString());
      }
    }

    // Finally the shared data
    {
      var sb = new IndentedStringBuilder(2);
      sb.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.cs");
      await ctx.Writer.WriteFileAsync("EVA.SDK.cs", sb.ToString());
    }

    if (ctx.Options.GenerateDynamicData)
    {
      var sb = new IndentedStringBuilder(2);
      sb.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.DynamicData.cs");
      await ctx.Writer.WriteFileAsync("EVA.SDK.DynamicData.cs", sb.ToString());
    }

    if (ctx.Options.GenerateDynamicData)
    {
      var resource = ctx.Options.UseSystemTextJson
        ? "dotnet.Resources.EVA.SDK.Core.DynamicData.SystemTextJson.cs"
        : "dotnet.Resources.EVA.SDK.Core.DynamicData.NewtonsoftJson.cs";
      var fileName = ctx.Options.UseSystemTextJson
        ? "EVA.SDK.DynamicData.SystemTextJson.cs"
        : "EVA.SDK.DynamicData.NewtonsoftJson.cs";

      var sb = new IndentedStringBuilder(2);
      sb.WriteManifestResourceStream(resource);
      await ctx.Writer.WriteFileAsync(fileName, sb.ToString());
    }

    // Serialization specific data
    {
      var resource = ctx.Options.UseSystemTextJson
        ? "dotnet.Resources.EVA.SDK.Core.SystemTextJson.cs"
        : "dotnet.Resources.EVA.SDK.Core.NewtonsoftJson.cs";
      var fileName = ctx.Options.UseSystemTextJson
        ? "EVA.SDK.SystemTextJson.cs"
        : "EVA.SDK.NewtonsoftJson.cs";

      var sb = new IndentedStringBuilder(2);
      sb.WriteManifestResourceStream(resource);
      await ctx.Writer.WriteFileAsync(fileName, sb.ToString());
    }

    // Extensions
    {
      var sb = new IndentedStringBuilder(2);
      if (ctx.Options.UseSystemTextJson)
      {
        sb.WriteLine("using System.Text.Json;");
        sb.WriteLine();
      }
      sb.WriteLine("namespace EVA.SDK;");
      WriteExtensionMethods(ctx, sb, state);
      await ctx.Writer.WriteFileAsync("EVA.SDK.Extensions.cs", sb.ToString());
    }
  }

  private void WriteExtensionMethods(ApiOutputContext<DotNetOptions> ctx, IndentedStringBuilder o, GenerationState state)
  {
    if (ctx.Options.GenerateDynamicData)
    {
      var converterType = ctx.Options.UseSystemTextJson
        ? "System.Text.Json.Serialization.JsonConverter"
        : "Newtonsoft.Json.JsonConverter";

      o.WriteLine("internal static class DynamicDataConverters");
      using (o.BracedIndentation)
      {
        o.WriteLine($"internal static readonly {converterType}[] Converters = new {converterType}[]");
        using (o.BracedIndentation)
        {
          foreach (var typeName in AllTypeReferences(ctx.Api)
                     .OfType<OptionRef>()
                     .Select(x => GetFullName(x.Shared.WithNullable(false), TypeContext.None, ctx, state))
                     .Distinct())
          {
            o.WriteLine($"new EVA.SDK.DynamicDataConverter<{typeName}>(),");
          }
        }

        o.WriteLine(";");
      }
    }

    o.WriteLine("public static class EVAExtensions");
    using (o.BracedIndentation)
    {
      if (ctx.Options.GenerateDynamicData)
      {
        foreach (var (classID, options) in state.AllOptionsImplementations)
        {
          var (_, fullTypeName) = state.AllocateName(classID);
          foreach (var opt in options.Select(x => GetFullName(x.WithNullable(false), TypeContext.None, ctx, state)).Distinct())
          {
            o.WriteLine($"public static {fullTypeName}? To{ctx.Api.GetType(classID).Name}(this EVA.SDK.DynamicData<{opt}>? obj)");
            using (o.BracedIndentation)
            {
              o.WriteLine(
                ctx.Options.UseSystemTextJson
                  ? $"return obj?.Data.Deserialize<{fullTypeName}>();"
                  : $"return obj?.Data.ToObject<{fullTypeName}>();"
              );
            }
          }
        }
      }

      if (ctx.Options.GenerateExtensions)
      {
        foreach(var service in ctx.Api.Services)
        {
          var (_, fullRequestName) = state.AllocateName(service.RequestTypeID);
          var (_, fullResponseName) = state.AllocateName(service.ResponseTypeID);

          var type = ctx.Api.GetType(service.RequestTypeID);
          if (type.Parent == null && type.Properties.Count == 0)
          {
            o.WriteLine($"public static System.Threading.Tasks.Task<{fullResponseName}> {service.Name}<TOptions>(this EVA.SDK.IEVAApiClient<TOptions> client, TOptions? options = default)");
            using (o.BracedIndentation)
            {
              o.WriteLine($"return client.CallService(new {fullRequestName}(), options);");
            }
          }

          o.WriteLine($"public static System.Threading.Tasks.Task<{fullResponseName}> {service.Name}<TOptions>(this EVA.SDK.IEVAApiClient<TOptions> client, {fullRequestName} request, TOptions? options = default)");
          using (o.BracedIndentation)
          {
            o.WriteLine("return client.CallService(request, options);");
          }
        }
      }
    }
  }

  private string? WriteType(string id, EvaType spec, IndentedStringBuilder sb, ApiOutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes, GenerationState state)
  {
    // These types have special handling
    if (ctx.Options.UseNativeDayOfWeek && id == DayOfWeek) return null;

    var (localName, fullName) = state.AllocateName(id);

    if (spec.Parent != null)
    {
      localName = localName[(localName.LastIndexOf('.') + 1)..];
    }

    if (spec.IsEnum)
    {
      if (spec.IsFlags) sb.WriteLine("[Flags]");
      sb.WriteLine($"public enum {spec.Name}");
      using (sb.BracedIndentation)
      {
        foreach (var value in spec.EnumValues.OrderBy(v => v.Name, StringComparer.Ordinal))
        {
          var (remainder, extends) = spec.IsFlags ? spec.FlagComposition(value) : (value.Value, []);
          var values = remainder == 0L && !extends.Any() ? new[] { "0" } : extends.Concat(new[] { remainder.ToString() });
          if (value.Description != null)
          {
            sb.WriteLine("/// <summary>");
            foreach (var l in value.Description.Trim().Split('\n').Select(x => $"/// {x.Trim()}"))
            {
              sb.WriteLine(l);
            }

            sb.WriteLine("/// </summary>");
          }

          sb.WriteLine($"{value.Name} = {string.Join(" | ", values)},");
        }
      }

      return fullName;
    }

    if (spec.TypeParameters.Any())
    {
      localName += $"<{string.Join(',', spec.TypeParameters)}>";
    }

    var inheritFromDynamic = (state.AllOptionsImplementations.GetValueOrDefault(id) ?? new List<TypeRef>())
      .Select(x => GetFullName(x.WithNullable(false), TypeContext.None, ctx, state))
      .Distinct()
      .ToList();

    var inheritFrom = inheritFromDynamic
      .Concat(allResponseTypes.Contains(id) ? new[] { "EVA.SDK.Core.IResponseMessage" } : Array.Empty<string>())
      .ToList();

    sb.WriteLine($"public class {localName}{(inheritFrom.Any() ? " : " + string.Join(", ", inheritFrom) : string.Empty)}");
    var usage = (spec.Usage.request ? TypeContext.Request : TypeContext.None) | (spec.Usage.response ? TypeContext.Response : TypeContext.None);
    using (sb.BracedIndentation)
    {
      WriteTypeBody(id, spec, sb, usage, ctx, allResponseTypes, state);
    }

    return fullName;
  }

  private string WriteResponseType(string id, EvaType spec, IndentedStringBuilder sb, ApiOutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes, GenerationState state)
  {
    var (localName, fullName) = state.AllocateName(id);
    sb.WriteLine($"public class {localName} : EVA.SDK.Core.IResponseMessage");
    using (sb.BracedIndentation)
    {
      WriteTypeBody(id, spec, sb, TypeContext.Response, ctx, allResponseTypes, state);
    }

    return fullName;
  }

  private string WriteRequestType(string id, EvaType requestType, IndentedStringBuilder o, EvaService service, ApiOutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes,
    GenerationState state)
  {
    var (localName, fullRequestName) = state.AllocateName(id);
    var (localResponseName, fullResponseName) = state.AllocateName(service.ResponseTypeID);

    o.WriteLine();

    WriteRequestTypeHeader(requestType, o, service);

    o.WriteLine($"public partial class {localName} : EVA.SDK.Core.IResponseType<{fullResponseName}>");
    using (o.BracedIndentation)
    {
      o.WriteLine($"[{JsonIgnoreAttribute(ctx.Options)}]");
      o.WriteLine($"public string Path => \"{service.Path.TrimStart('/')}\";");
      o.WriteLine();
      WriteTypeBody(service.RequestTypeID, requestType, o, TypeContext.Request, ctx, allResponseTypes, state);
    }

    return fullRequestName;
  }

  private static void WriteRequestTypeHeader(EvaType requestType, IndentedStringBuilder o, EvaService service)
  {
    if (requestType.Description != null)
    {
      o.WriteLine("/// <summary>");
      o.WriteLines(requestType.Description, "/// ");
      o.WriteLine("/// </summary>");
    }

    o.WriteLine("/// <remarks>");
    var auth = service.Auth;
    {
      o.WriteLine($"/// Required user type: {auth.UserTypes?.ToString() ?? "None"}");
      if (auth.Functionality != null) o.WriteLine($"/// Required permission: {auth.Functionality} (Scope: {auth.Scope})");
    }

    o.WriteLine("/// </remarks>");
  }

  private void WriteTypeBody(string id, EvaType spec, IndentedStringBuilder o, TypeContext context, ApiOutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes,
    GenerationState state)
  {
    foreach (var prop in spec.Properties)
    {
      if (prop.Value.Description != null)
      {
        o.WriteLine("/// <summary>");
        o.WriteLines(prop.Value.Description, "/// ");
        o.WriteLine("/// </summary>");
      }

      if (prop.Value.Entity is { } entity)
      {
        o.WriteLine("/// <remarks>");
        o.WriteLine($"/// Entity type: {entity.Name}");
        if (context.HasFlag(TypeContext.Request) && entity.BackendID)
        {
          o.WriteLine($"/// Supports passing in the BackendID of a {entity.Name} when using EVA-IDs-Mode: ExternalIDs.");
          if (entity.Lenient) o.WriteLine("/// Allows missing/invalid values when using EVA-IDs-Mode: ExternalIDs.");
        }

        o.WriteLine("/// </remarks>");
      }

      if (prop.Value.Deprecated != null)
      {
        o.WriteLine($"[Obsolete(@\"{prop.Value.Deprecated.Comment?.Replace("\"", "\"\"")}\")]");
      }

      if (prop.Value.Skippable)
      {
        o.WriteLine("[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
        if (ctx.Options.UseSystemTextJson)
        {
          o.WriteLine(
            "[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]"
          );
        }
        else
        {
          o.WriteLine($"public bool ShouldSerialize{prop.Key}() => {prop.Key}.IsValuePresent;");
        }

        var nameForConverter = GetFullName(prop.Value.Type, context, ctx, state);
        o.WriteLine($"[{JsonConverterAttribute(ctx.Options)}(typeof(EVA.SDK.MaybeConverter<{nameForConverter}>))]");
      }

      var fullPropName = GetFullPropName(context, ctx, prop.Value, state);
      o.WriteLine($"public {fullPropName} {prop.Key} {{ get; set; }}");
    }

    foreach (var ts in ctx.Api.Types.Where(t => t.Value.Parent?.ID == id))
    {
      WriteType(ts.Key, ts.Value, o, ctx, allResponseTypes, state);
    }
  }

  private static string GetFullPropName(TypeContext context, ApiOutputContext<DotNetOptions> ctx, EvaProperty prop, GenerationState state)
  {
    var fullPropName = GetFullName(prop.Type, context, ctx, state);

    if (prop.Skippable)
    {
      fullPropName = $"EVA.SDK.Core.Maybe<{fullPropName}>";
    }

    return fullPropName;
  }

  private static string GetFullName(TypeRef r, TypeContext context, ApiOutputContext<DotNetOptions> ctx, GenerationState state)
  {
    var n = r.Nullable ? "?" : string.Empty;
    var stj = ctx.Options.UseSystemTextJson;

    // IDs are always strings
    if (r is PrimitiveRef { Kind: PrimitiveKind.Guid }) return $"System.Guid{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.String or PrimitiveKind.ID }) return $"string{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.Int32 }) return $"int{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.Int16 }) return $"short{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.Bool }) return $"bool{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.Binary }) return $"byte[]{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.Decimal }) return $"decimal{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.Float64 }) return $"double{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.Float32 }) return $"float{n}";

    // We'll expose the shared part of the option
    if (r is OptionRef option)
    {
      var name = GetFullName(option.Shared.WithNullable(false), context, ctx, state);
      // if (!context.HasFlag(TypeContext.Response)) return name;
      if (ctx.Options.GenerateDynamicData)
      {
        return $"EVA.SDK.DynamicData<{name}>{n}";
      }
      else
      {
        return $"{name}{n}";
      }
    }

    if (r is PrimitiveRef { Kind: PrimitiveKind.Int64 })
    {
      return $"long{n}";
    }

    if (r is ArrayRef array)
    {
      var type = (context & TypeContext.Request) != 0 ? "IEnumerable" : "List";
      return $"{type}<{GetFullName(array.Items, context, ctx, state)}>{n}";
    }

    if (r is PrimitiveRef { Kind: PrimitiveKind.Date }) return $"System.DateTime{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.Duration }) return $"TimeSpan{n}";

    // Special case for type IDictionary<string, object?>
    if (r is MapRef { Key: PrimitiveRef { Kind: PrimitiveKind.String or PrimitiveKind.ID }, Value: PrimitiveRef { Kind: PrimitiveKind.Any } })
    {
      if (stj)
      {
        return (context & TypeContext.Request) != 0 ? $"IDictionary<string, JsonNode>{n}" : $"JsonObject{n}";
      }

      return (context & TypeContext.Request) != 0 ? $"IDictionary<string, JToken>{n}" : $"JObject{n}";
    }

    if (r is MapRef { Key: PrimitiveRef { Kind: PrimitiveKind.Int64 }, Value: PrimitiveRef { Kind: PrimitiveKind.Any } })
    {
      return stj ? $"IDictionary<long, JsonNode>{n}" : $"IDictionary<long, JToken>{n}";
    }

    if (r is MapRef map)
    {
      return $"IDictionary<{GetFullName(map.Key.WithNullable(false), context, ctx, state)},{GetFullName(map.Value, context, ctx, state)}>{n}";
    }

    if (r is PrimitiveRef { Kind: PrimitiveKind.Object }) return stj ? $"JsonObject{n}" : $"JObject{n}";
    if (r is NamedRef { TypeID: IProductSearchItem }) return stj ? $"JsonObject{n}" : $"JObject{n}";
    if (r is PrimitiveRef { Kind: PrimitiveKind.Any }) return (context & TypeContext.Request) != 0 ? $"object{n}" : stj ? $"JsonNode{n}" : $"JToken{n}";
    if (ctx.Options.UseNativeDayOfWeek && r is NamedRef { TypeID: DayOfWeek }) return $"DayOfWeek{n}";

    if (r is NamedRef named)
    {
      var (_, fName) = state.AllocateName(named.TypeID);

      if (!named.Arguments.Any()) return fName + n;

      var joinedArguments = string.Join(',', named.Arguments.Select(a => GetFullName(a, context, ctx, state)));
      return $"{fName}<{joinedArguments}>{n}";
    }

    if (r is TypeParameterRef parameter)
    {
      return parameter.Name + n;
    }

    ctx.Logger.LogWarning("Type cannot be handled by this output: {Type}, outputting as \"object\"", r.SpecName());
    return "object";
  }

  private static string FixNamespace(string ns)
  {
    return ns.Replace("EVA.", "EVA.SDK.");
  }

  private static string JsonTypesUsing(DotNetOptions options) =>
    options.UseSystemTextJson ? "using System.Text.Json.Nodes;" : "using Newtonsoft.Json.Linq;";

  private static string JsonIgnoreAttribute(DotNetOptions options) =>
    options.UseSystemTextJson ? "System.Text.Json.Serialization.JsonIgnore" : "Newtonsoft.Json.JsonIgnore";

  private static string JsonConverterAttribute(DotNetOptions options) =>
    options.UseSystemTextJson
      ? "System.Text.Json.Serialization.JsonConverter"
      : "Newtonsoft.Json.JsonConverter";
}
