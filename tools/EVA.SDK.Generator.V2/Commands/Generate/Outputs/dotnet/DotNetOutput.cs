using System.Text;
using System.Text.Json;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;

internal class DotNetOutput : IOutput<DotNetOptions>
{
  private class GenerationState
  {
    public Dictionary<string, List<TypeReference?>> AllOptionsImplementations;
    public ApiDefinitionModel Input;

    private Dictionary<string, (string local, string full)> typeNameCache = [];
    private HashSet<(string ns, string local)> allocatedFullTypeNames = [];


    public (string localName, string fullName) AllocateName(string id)
    {
      if (typeNameCache.TryGetValue(id, out var x)) return (x.local, x.full);

      // Build the local name for the NS
      var spec = Input.Types[id];
      var localName = spec.TypeName.Split('`')[0];

      if (spec.ParentType != null)
      {
        var (parentLocal, _) = AllocateName(spec.ParentType);
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

  public bool GetForcedTransformations(DotNetOptions options, INamedTransform x) =>
    options.GenerateDynamicData
      ? x is RemoveEventExports or RemoveDataLakeExports or RemoveInheritance
      : x is RemoveEventExports or RemoveDataLakeExports or RemoveInheritance or RemoveOptions;

  private static void WriteErrors(ApiDefinitionModelExtensions.PrefixGroupedErrors errors, IndentedStringBuilder o, string name)
  {
    if (!errors.Errors.Any() && !errors.SubErrors.Any()) return;

    o.WriteLine($"public static class {name}");
    using (o.BracedIndentation)
    {
      foreach (var e in errors.Errors)
      {
        o.WriteLines(e.error.MessageWithEnhancedArguments(), "/// ");
        o.WriteLine($"public const string {e.Name} = \"{e.error.Name}\";");
      }

      foreach (var (prefix, suberrors) in errors.SubErrors)
      {
        WriteErrors(suberrors, o, prefix);
      }
    }
  }

  public async Task Write(OutputContext<DotNetOptions> ctx)
  {
    var allResponseTypes = ctx.Input.Services.Select(s => s.ResponseTypeID).ToHashSet();

    // Some preprocessing
    var state = new GenerationState
    {
      Input = ctx.Input,
      AllOptionsImplementations = ctx.Input.EnumerateAllTypeReferences()
        .Where(x => x.Name == ApiSpecConsts.Specials.Option)
        .SelectMany(x => x.Arguments.Select(a => (x.Shared, a.Name)))
        .GroupBy(x => x.Name)
        .ToDictionary(x => x.Key, x => x.Select(y => y.Shared).ToList())
    };

    if (ctx.Options.AddEvaClient)
    {
      var content = ManifestResourceHelpers.GetResource("dotnet.Resources.EVAClient.cs")!.Replace("<<API_VERSION>>", ctx.Input.ApiVersion.ToString());

      await ctx.Writer.WriteFileAsync("EVAClient.cs", content);
    }

    await ctx.Writer.WriteFileAsync("GlobalUsings.cs", ManifestResourceHelpers.GetResource("dotnet.Resources.GlobalUsings.cs")!);

    var handledTypes = new HashSet<string>();

    // First we write out the requests
    foreach(var service in ctx.Input.Services)
    {
      var requestType = ctx.Input.Types[service.RequestTypeID];
      var actualNamespace = FixNamespace(requestType.Assembly);
      var sb = new IndentedStringBuilder(2);

      sb.WriteLine("#pragma warning disable CS8618");
      sb.WriteLine("using Newtonsoft.Json.Linq;");
      sb.WriteLine();

      sb.WriteLine($"namespace {actualNamespace};");
      sb.WriteLine();

      var fullRequestTypeName = WriteRequestType(service.RequestTypeID, requestType, sb, service, ctx, allResponseTypes, state);
      handledTypes.Add(service.RequestTypeID);

      await ctx.Writer.WriteFileAsync($"requests/{fullRequestTypeName}.cs", sb.ToString());
    }

    // Next we write out the responses
    foreach(var service in ctx.Input.Services)
    {
      if (!handledTypes.Add(service.ResponseTypeID)) continue;

      var responseType = ctx.Input.Types[service.ResponseTypeID];
      var actualNamespace = FixNamespace(responseType.Assembly);
      var sb = new IndentedStringBuilder(2);

      sb.WriteLine("#pragma warning disable CS8618");
      sb.WriteLine("using Newtonsoft.Json.Linq;");
      sb.WriteLine();

      sb.WriteLine($"namespace {actualNamespace};");
      sb.WriteLine();

      var fullResponseTypeName = WriteResponseType(service.ResponseTypeID, responseType, sb, ctx, allResponseTypes, state);

      await ctx.Writer.WriteFileAsync($"responses/{fullResponseTypeName}.cs", sb.ToString());
    }

    // As the following step, we write out all the remaining types
    foreach (var type in ctx.Input.Types.Where(type => type.Value.ParentType == null))
    {
      if(!handledTypes.Add(type.Key)) continue;

      var actualNamespace = FixNamespace(type.Value.Assembly);
      var sb = new IndentedStringBuilder(2);

      sb.WriteLine("#pragma warning disable CS8618");
      sb.WriteLine("using Newtonsoft.Json.Linq;");

      sb.WriteLine();
      sb.WriteLine($"namespace {actualNamespace};");
      sb.WriteLine();

      var fullTypeName = WriteType(type.Key, type.Value, sb, ctx, allResponseTypes, state);
      if (fullTypeName == null) continue;

      await ctx.Writer.WriteFileAsync($"types/{fullTypeName}.cs", sb.ToString());
    }

    // We also need the list of possible errors
    {
      foreach (var group in ctx.Input.Errors.GroupBy(x => x.Assembly))
      {
        var sb = new IndentedStringBuilder(2);
        var ns = FixNamespace(group.Key);
        sb.WriteLine($"namespace {ns};");
        sb.WriteLine();
        WriteErrors(group.GroupByPrefix(), sb, "Errors");
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
      var sb = new IndentedStringBuilder(2);
      sb.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.DynamicData.NewtonsoftJson.cs");
      await ctx.Writer.WriteFileAsync("EVA.SDK.DynamicData.NewtonsoftJson.cs", sb.ToString());
    }

    // Serialization specific data
    {
      var sb = new IndentedStringBuilder(2);
      sb.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.NewtonsoftJson.cs");
      await ctx.Writer.WriteFileAsync("EVA.SDK.NewtonsoftJson.cs", sb.ToString());
    }

    // Extensions
    {
      var sb = new IndentedStringBuilder(2);
      sb.WriteLine("namespace EVA.SDK;");
      WriteExtensionMethods(ctx, sb, state);
      await ctx.Writer.WriteFileAsync("EVA.SDK.Extensions.cs", sb.ToString());
    }
  }

  private void WriteExtensionMethods(OutputContext<DotNetOptions> ctx, IndentedStringBuilder o, GenerationState state)
  {
    if (ctx.Options.GenerateDynamicData)
    {
      o.WriteLine("internal static class DynamicDataConverters");
      using (o.BracedIndentation)
      {
        o.WriteLine("internal static readonly Newtonsoft.Json.JsonConverter[] Converters = new Newtonsoft.Json.JsonConverter[]");
        using (o.BracedIndentation)
        {
          foreach (var typeName in ctx.Input.EnumerateAllTypeReferences()
                     .Where(x => x.Name == ApiSpecConsts.Specials.Option)
                     .Select(x => GetFullName(x.Shared.CloneAsNotNull(), TypeContext.None, ctx, state))
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
          foreach (var opt in options.Select(x => GetFullName(x.CloneAsNotNull(), TypeContext.None, ctx, state)).Distinct())
          {
            o.WriteLine($"public static {fullTypeName}? To{ctx.Input.Types[classID].TypeName}(this EVA.SDK.DynamicData<{opt}>? obj)");
            using (o.BracedIndentation)
            {
              o.WriteLine($"return obj?.Data.ToObject<{fullTypeName}>();");
            }
          }
        }
      }

      if (ctx.Options.GenerateExtensions)
      {
        foreach(var service in ctx.Input.Services)
        {
          var (_, fullRequestName) = state.AllocateName(service.RequestTypeID);
          var (_, fullResponseName) = state.AllocateName(service.ResponseTypeID);

          var type = ctx.Input.Types[service.RequestTypeID];
          if (type.ParentType == null && type.Properties.Count == 0)
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

  private string? WriteType(string id, TypeSpecification spec, IndentedStringBuilder sb, OutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes, GenerationState state)
  {
    // These types have special handling
    if (ctx.Options.UseNativeDayOfWeek && id == ApiSpecConsts.WellKnown.DayOfWeek) return null;

    var (localName, fullName) = state.AllocateName(id);

    if (spec.ParentType != null)
    {
      localName = localName[(localName.LastIndexOf('.') + 1)..];
    }

    if (spec.EnumIsFlag.HasValue)
    {
      if (spec.EnumIsFlag.Value) sb.WriteLine("[Flags]");
      sb.WriteLine($"public enum {spec.TypeName}");
      using (sb.BracedIndentation)
      {
        foreach (var (name, value) in spec.EnumValues)
        {
          var values = value.Value == 0L && !value.Extends.Any() ? new[] { "0" } : value.Extends.Concat(new[] { value.Value.ToString() });
          if (value.Description != null)
          {
            sb.WriteLine("/// <summary>");
            foreach (var l in value.Description.Trim().Split('\n').Select(x => $"/// {x.Trim()}"))
            {
              sb.WriteLine(l);
            }

            sb.WriteLine("/// </summary>");
          }

          sb.WriteLine($"{name} = {string.Join(" | ", values)},");
        }
      }

      return fullName;
    }

    if (spec.TypeArguments.Any())
    {
      localName += $"<{string.Join(',', spec.TypeArguments.Select(x => x[1..]))}>";
    }

    var inheritFromDynamic = (state.AllOptionsImplementations.GetValueOrDefault(id) ?? new List<TypeReference?>())
      .Select(x => GetFullName(x.CloneAsNotNull(), TypeContext.None, ctx, state))
      .Distinct()
      .ToList();

    var inheritFrom = inheritFromDynamic
      .Concat(allResponseTypes.Contains(id) ? new[] { "EVA.SDK.Core.IResponseMessage" } : Array.Empty<string>())
      .ToList();

    sb.WriteLine($"public class {localName}{(inheritFrom.Any() ? " : " + string.Join(", ", inheritFrom) : string.Empty)}");
    var usage = (spec.Usage.Request ? TypeContext.Request : TypeContext.None) | (spec.Usage.Response ? TypeContext.Response : TypeContext.None);
    using (sb.BracedIndentation)
    {
      WriteTypeBody(id, spec, sb, usage, ctx, allResponseTypes, state);
    }

    return fullName;
  }

  private string WriteResponseType(string id, TypeSpecification spec, IndentedStringBuilder sb, OutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes, GenerationState state)
  {
    var (localName, fullName) = state.AllocateName(id);
    sb.WriteLine($"public class {localName} : EVA.SDK.Core.IResponseMessage");
    using (sb.BracedIndentation)
    {
      WriteTypeBody(id, spec, sb, TypeContext.Response, ctx, allResponseTypes, state);
    }

    return fullName;
  }

  private string WriteRequestType(string id, TypeSpecification requestType, IndentedStringBuilder o, ServiceModel service, OutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes,
    GenerationState state)
  {
    var (localName, fullRequestName) = state.AllocateName(id);
    var (localResponseName, fullResponseName) = state.AllocateName(service.ResponseTypeID);

    o.WriteLine();

    WriteRequestTypeHeader(requestType, o, service);

    o.WriteLine($"public partial class {localName} : EVA.SDK.Core.IResponseType<{fullResponseName}>");
    using (o.BracedIndentation)
    {
      o.WriteLine($"[{DotNetNames.JsonIgnore}]");
      o.WriteLine($"public string Path => \"{service.Path.TrimStart('/')}\";");
      o.WriteLine();
      WriteTypeBody(service.RequestTypeID, requestType, o, TypeContext.Request, ctx, allResponseTypes, state);
    }

    return fullRequestName;
  }

  private static void WriteRequestTypeHeader(TypeSpecification requestType, IndentedStringBuilder o, ServiceModel service)
  {
    if (requestType.Description != null)
    {
      o.WriteLine("/// <summary>");
      o.WriteLines(requestType.Description, "/// ");
      o.WriteLine("/// </summary>");
    }

    o.WriteLine("/// <remarks>");
    foreach (var auth in service.AuthInformation.RequiredPermissions)
    {
      o.WriteLine($"/// Required user type: {auth.UserTypes?.ToString() ?? "None"}");
      if (auth.Functionality != null) o.WriteLine($"/// Required permission: {auth.Functionality} (Scope: {auth.Scope})");
    }

    o.WriteLine("/// </remarks>");
  }

  private void WriteTypeBody(string id, TypeSpecification spec, IndentedStringBuilder o, TypeContext context, OutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes,
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

      if (prop.Value.DataModelInformation != null)
      {
        o.WriteLine("/// <remarks>");
        o.WriteLine($"/// Entity type: {prop.Value.DataModelInformation.Name}");
        if (context.HasFlag(TypeContext.Request) && prop.Value.DataModelInformation.SupportsBackendID)
        {
          o.WriteLine($"/// Supports passing in the BackendID of a {prop.Value.DataModelInformation.Name} when using EVA-IDs-Mode: ExternalIDs.");
          if (prop.Value.DataModelInformation.Lenient) o.WriteLine("/// Allows missing/invalid values when using EVA-IDs-Mode: ExternalIDs.");
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
        o.WriteLine($"public bool ShouldSerialize{prop.Key}() => {prop.Key}.IsValuePresent;");

        var nameForConverter = GetFullName(prop.Value.Type, context, ctx, state);
        o.WriteLine($"[Newtonsoft.Json.JsonConverter(typeof(EVA.SDK.MaybeConverter<{nameForConverter}>))]");
      }

      var fullPropName = GetFullPropName(context, ctx, prop.Value, state);
      o.WriteLine($"public {fullPropName} {prop.Key} {{ get; set; }}");
    }

    foreach (var ts in ctx.Input.Types.Where(t => t.Value.ParentType == id))
    {
      WriteType(ts.Key, ts.Value, o, ctx, allResponseTypes, state);
    }
  }

  private static string GetFullPropName(TypeContext context, OutputContext<DotNetOptions> ctx, PropertySpecification prop, GenerationState state)
  {
    var fullPropName = GetFullName(prop.Type, context, ctx, state);

    if (prop.Skippable)
    {
      fullPropName = $"EVA.SDK.Core.Maybe<{fullPropName}>";
    }

    return fullPropName;
  }

  private static string GetFullName(TypeReference r, TypeContext context, OutputContext<DotNetOptions> ctx, GenerationState state)
  {
    var n = r.Nullable ? "?" : string.Empty;

    if (r.Name == ApiSpecConsts.Guid) return $"System.Guid{n}";
    if (r.Name == ApiSpecConsts.String) return $"string{n}";
    if (r.Name == ApiSpecConsts.ID) return $"long{n}";
    if (r.Name == ApiSpecConsts.Int32) return $"int{n}";
    if (r.Name == ApiSpecConsts.Int16) return $"short{n}";
    if (r.Name == ApiSpecConsts.Bool) return $"bool{n}";
    if (r.Name == ApiSpecConsts.Binary) return $"byte[]{n}";
    if (r.Name == ApiSpecConsts.Float128) return $"decimal{n}";
    if (r.Name == ApiSpecConsts.Float64) return $"double{n}";
    if (r.Name == ApiSpecConsts.Float32) return $"float{n}";

    // We'll expose the shared part of the option
    if (r.Name == ApiSpecConsts.Specials.Option)
    {
      var name = GetFullName(r.Shared.CloneAsNotNull(), context, ctx, state);
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

    if (r.Name == ApiSpecConsts.Int64)
    {
      return $"long{n}";
    }

    if (r.Name == ApiSpecConsts.Specials.Array)
    {
      var type = (context & TypeContext.Request) != 0 ? "IEnumerable" : "List";
      return $"{type}<{GetFullName(r.Arguments[0], context, ctx, state)}>{n}";
    }

    if (r.Name == ApiSpecConsts.Date) return $"System.DateTime{n}";
    if (r.Name == ApiSpecConsts.Duration) return $"TimeSpan{n}";

    // Special case for type IDictionary<string, object?>
    if (r is { Name: ApiSpecConsts.Specials.Map, Arguments: [{ Name: ApiSpecConsts.String }, { Name: ApiSpecConsts.Any }] })
    {
      return (context & TypeContext.Request) != 0 ? $"IDictionary<string, JToken>{n}" : $"JObject{n}";
    }

    if (r is { Name: ApiSpecConsts.Specials.Map, Arguments: [{ Name: ApiSpecConsts.Int64 }, { Name: ApiSpecConsts.Any }] })
    {
      return $"IDictionary<long, JToken>{n}";
    }

    if (r.Name == ApiSpecConsts.Specials.Map)
    {
      return $"IDictionary<{GetFullName(r.Arguments[0].CloneAsNotNull(), context, ctx, state)},{GetFullName(r.Arguments[1], context, ctx, state)}>{n}";
    }

    if (r.Name == ApiSpecConsts.Object) return $"JObject{n}";
    if (r.Name == ApiSpecConsts.WellKnown.IProductSearchItem) return $"JObject{n}";
    if (r.Name == ApiSpecConsts.Any) return (context & TypeContext.Request) != 0 ? $"object{n}" : $"JToken{n}";
    if (ctx.Options.UseNativeDayOfWeek && r.Name == ApiSpecConsts.WellKnown.DayOfWeek) return $"DayOfWeek{n}";

    if (r.Name.StartsWith("EVA."))
    {
      var name = r.Name;
      var (_, fName) = state.AllocateName(name);

      if (!r.Arguments.Any()) return fName + n;

      var joinedArguments = string.Join(',', r.Arguments.Select(a => GetFullName(a, context, ctx, state)));
      return $"{fName}<{joinedArguments}>{n}";
    }

    if (r.Name.StartsWith("_"))
    {
      return r.Name[1..] + n;
    }

    ctx.Logger.LogWarning("Type cannot be handled by this output: {Type}, outputting as \"object\"", r.Name);
    return "object";
  }

  private static string FixNamespace(string ns)
  {
    return ns.Replace("EVA.", "EVA.SDK.");
  }
}