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

    private Dictionary<string, (string local, string full)> typeNameCache = new();
    private HashSet<(string ns, string local)> allocatedFullTypeNames = new();
    public HashSet<string> AllOptionsInterfaces;

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

  public string OutputPattern => "EVA.*.cs";
  public string[] ForcedTransformations => new[] { RemoveEventExports.ID, RemoveDataLakeExports.ID, RemoveInheritance.ID };

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
    var groupedInput = ctx.Input.GroupByAssembly();
    var allResponseTypes = ctx.Input.Services.Select(s => s.ResponseTypeID).ToHashSet();

    // Some preprocessing
    var state = new GenerationState
    {
      Input = ctx.Input,
      AllOptionsImplementations = ctx.Input.EnumerateAllTypeReferences()
        .Where(x => x.Name == ApiSpecConsts.Specials.Option)
        .SelectMany(x => x.Arguments.Select(a => (x.Shared, a.Name)))
        .GroupBy(x => x.Name)
        .ToDictionary(x => x.Key, x => x.Select(y => y.Shared).ToList()),
      AllOptionsInterfaces = ctx.Input.EnumerateAllTypeReferences()
        .Where(x => x.Name == ApiSpecConsts.Specials.Option)
        .Where(x => x.Shared != null)
        .Select(x => x.Shared.Name)
        .ToHashSet()
    };

    foreach (var i in groupedInput)
    {
      var handledTypes = new HashSet<string>();

      var actualNamespace = FixNamespace(i.Assembly);
      var sb = new IndentedStringBuilder(2);

      sb.WriteLine("#nullable enable");
      sb.WriteLine();
      sb.WriteLine("using System;");
      sb.WriteLine("using System.Collections;");
      sb.WriteLine("using System.Collections.Generic;");
      sb.WriteLine("using System.ComponentModel;");
      sb.WriteLine("using System.Linq;");
      if (ctx.Options.JsonSerializer == "newtonsoft")
      {
        sb.WriteLine("using System.Reflection;");
        sb.WriteLine("using Newtonsoft.Json;");
        sb.WriteLine("using Newtonsoft.Json.Serialization;");
        sb.WriteLine("using Newtonsoft.Json.Linq;");
      }

      sb.WriteLine();
      sb.WriteLine($"namespace {actualNamespace}");
      using (sb.BracedIndentation)
      {
        if (i.Assembly == ApiSpecConsts.WellKnown.CoreAssembly)
        {
          sb.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.cs");
          if (ctx.Options.JsonSerializer == "newtonsoft")
          {
            sb.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.NewtonsoftJson.cs");
          }
        }

        WriteErrors(i.Errors.GroupByPrefix(), sb, "Errors");

        foreach (var service in i.Services)
        {
          var requestType = ctx.Input.Types[service.RequestTypeID];
          var responseType = ctx.Input.Types[service.ResponseTypeID];

          sb.WriteLine();
          WriteRequestType(service.RequestTypeID, requestType, sb, service, ctx, allResponseTypes, state);
          handledTypes.Add(service.RequestTypeID);
          sb.WriteLine();

          // Write response
          if (responseType.Assembly == service.Assembly && handledTypes.Add(service.ResponseTypeID))
          {
            WriteResponseType(service.ResponseTypeID, responseType, sb, ctx, allResponseTypes, state);
          }
        }

        WriteExtensionMethods(i.Services, ctx, sb, state, i.Assembly);

        foreach (var type in i.Types.Where(type => !handledTypes.Contains(type.Key) && type.Value.ParentType == null))
        {
          sb.WriteLine();
          WriteType(type.Key, type.Value, sb, ctx, allResponseTypes, state);
        }
      }

      await ctx.Writer.WriteFileAsync($"{actualNamespace}.cs", sb.ToString());
    }
  }

  private void WriteExtensionMethods(List<ServiceModel> services, OutputContext<DotNetOptions> ctx, IndentedStringBuilder o, GenerationState state, string ns)
  {
    o.WriteLine("public static class EVAExtensions");
    using (o.BracedIndentation)
    {
      foreach (var (classID, options) in state.AllOptionsImplementations)
      {
        if (ctx.Input.Types[classID].Assembly != ns) continue;

        var (_, fullTypeName) = state.AllocateName(classID);
        foreach (var opt in options.Select(x => GetFullName(x.CloneAsNotNull(), TypeContext.None, ctx, state)).Distinct())
        {
          o.WriteLine($"public static {fullTypeName} To{ctx.Input.Types[classID].TypeName}(this EVA.SDK.Core.DynamicData<{opt}> obj)");
          using (o.BracedIndentation)
          {
            o.WriteLine($"return obj.Data.ToObject<{fullTypeName}>();");
          }
        }
      }

      foreach (var service in services)
      {
        var (_, resType) = state.AllocateName(service.ResponseTypeID);
        var (_, reqType) = state.AllocateName(service.RequestTypeID);

        var requestType = ctx.Input.Types[service.RequestTypeID];
        if (requestType.Properties.Count > 1) continue;

        o.WriteLine($"public static System.Threading.Tasks.Task<{resType}> {service.Name}<TOptions>(this EVA.SDK.Core.IEVAClient<TOptions> client,");
        using (o.Indentation)
        {
          foreach (var property in requestType.Properties.OrderBy(p => p.Value.Type.Nullable ? 1 : 0))
          {
            var propName = GetFullPropName(TypeContext.Request, ctx, property.Value, state);
            o.WriteLine($"{propName} {property.Key}{(property.Value.Type.Nullable ? " = default" : "")},");
          }

          o.WriteLine(" TOptions options = default)");
        }

        using (o.BracedIndentation)
        {
          o.WriteLine($"var request = new {reqType}");
          o.WriteLine("{");
          using (o.Indentation)
          {
            foreach (var p in requestType.Properties)
            {
              o.WriteLine($"{p.Key} = {p.Key},");
            }
          }

          o.WriteLine("};");

          o.WriteLine("return client.CallService(request, options);");
        }
      }
    }
  }

  private void WriteType(string id, TypeSpecification spec, IndentedStringBuilder sb, OutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes, GenerationState state)
  {
    // These types have special handling
    if (ctx.Options.UseNativeDayOfWeek && id == ApiSpecConsts.WellKnown.DayOfWeek) return;

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

      return;
    }

    var (localName, _) = state.AllocateName(id);
    if (spec.ParentType != null)
    {
      localName = localName[(localName.LastIndexOf('.') + 1)..];
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

    var isInterface = state.AllOptionsInterfaces.Contains(id);
    sb.WriteLine($"public {(isInterface ? "interface" : "class")} {localName}{(inheritFrom.Any() ? " : " + string.Join(", ", inheritFrom) : string.Empty)}");
    var usage = (spec.Usage.Request ? TypeContext.Request : TypeContext.None) | (spec.Usage.Response ? TypeContext.Response : TypeContext.None);
    using (sb.BracedIndentation)
    {
      WriteTypeBody(id, localName, spec, sb, usage, ctx, allResponseTypes, state);

      // if (isInterface)
      // {
      //   sb.WriteLine($"public static implicit operator EVA.SDK.Core.DynamicData<{localName}>({localName} x)");
      //   using (sb.BracedIndentation)
      //   {
      //     sb.WriteLine($"return new EVA.SDK.Core.DynamicData<{localName}> {{ Data = JObject.FromObject(x) }};");
      //   }
      // }
    }
  }

  private void WriteResponseType(string id, TypeSpecification spec, IndentedStringBuilder sb, OutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes, GenerationState state)
  {
    var (localName, _) = state.AllocateName(id);
    sb.WriteLine($"public class {localName} : EVA.SDK.Core.IResponseMessage");
    using (sb.BracedIndentation)
    {
      WriteTypeBody(id, localName, spec, sb, TypeContext.Response, ctx, allResponseTypes, state);
    }
  }

  private void WriteRequestType(string id, TypeSpecification requestType, IndentedStringBuilder o, ServiceModel service, OutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes, GenerationState state)
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

    var (localName, _) = state.AllocateName(id);
    var (_, fullResponseName) = state.AllocateName(service.ResponseTypeID);

    o.WriteLine("/// </remarks>");
    o.WriteLine($"[EVA.SDK.Core.RequestMessage(\"{service.Path}\")]");
    o.WriteLine($"public class {localName} : EVA.SDK.Core.IResponseType<{fullResponseName}>");
    using (o.BracedIndentation)
    {
      WriteTypeBody(service.RequestTypeID, localName, requestType, o, TypeContext.Request, ctx, allResponseTypes, state);
    }
  }

  private void WriteTypeBody(string id, string className, TypeSpecification spec, IndentedStringBuilder o, TypeContext context, OutputContext<DotNetOptions> ctx, HashSet<string> allResponseTypes, GenerationState state)
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
          o.WriteLine("/// Supports passing in the BackendID as a string.");
          if (prop.Value.DataModelInformation.Lenient) o.WriteLine("/// Allows missing/invalid values when using EVA-IDs-Mode: ExternalIDs.");
        }

        o.WriteLine("/// </remarks>");
      }

      if (prop.Value.Deprecated != null)
      {
        o.WriteLine($"[Obsolete(@\"{prop.Value.Deprecated.Comment?.Replace("\"", "\"\"")}\")]");
      }

      var fullPropName = GetFullPropName(context, ctx, prop.Value, state);

      o.WriteLine($"public {fullPropName} {prop.Key} {{ get; set; }}");
    }

    // if (state.AllOptionsImplementations.TryGetValue(id, out var options))
    // {
    //   foreach (var x in options.Select(x => GetFullName(x.CloneAsNotNull(), TypeContext.None, ctx, state)).Distinct())
    //   {
    //     o.WriteLine($"public static implicit operator EVA.SDK.Core.DynamicData<{x}>({className} x)");
    //     using (o.BracedIndentation)
    //     {
    //       o.WriteLine($"return new EVA.SDK.Core.DynamicData<{x}> {{ Data = JObject.FromObject(x) }};");
    //     }
    //   }
    // }

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
      var name = GetFullName(r.Shared, context, ctx, state);
      // if (!context.HasFlag(TypeContext.Response)) return name;
      return $"EVA.SDK.Core.DynamicData<{name}>{n}";
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