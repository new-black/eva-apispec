using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;

internal class DotNetOutput : IOutput<DotNetOptions>
{
  [Flags]
  public enum TypeContext
  {
    None = 0,
    Request = 1,
    Response = 2
  }

  public string OutputPattern => "EVA.*.cs";
  public string[] ForcedRemoves => new[] { "options", "event-exports" };

  private static void WriteErrors(ApiDefinitionModelExtensions.PrefixGroupedErrors errors, IndentedStringBuilder o, string name)
  {
    if (!errors.Errors.Any() && !errors.SubErrors.Any()) return;

    o.WriteLine($"public static class {name}");
    o.WriteLine("{");
    using (o.Indentation)
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
    o.WriteLine("}");
  }

  public async Task Write(OutputContext<DotNetOptions> ctx)
  {
    var groupedInput = ctx.Input.GroupByAssembly();

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
      if (ctx.Options.JsonSerializer == "newtonsoft") sb.WriteLine("using Newtonsoft.Json.Linq;");
      sb.WriteLine();
      sb.WriteLine($"namespace {actualNamespace}");
      sb.WriteLine("{");
      sb.WriteIndented(o =>
      {
        if (i.Assembly == ApiSpecConsts.WellKnown.CoreAssembly)
        {
          o.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.cs");
          if (ctx.Options.JsonSerializer == "newtonsoft")
          {
            o.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.NewtonsoftJson.cs");
          }
        }

        WriteErrors(i.Errors.GroupByPrefix(), o, "Errors");

        foreach (var service in i.Services)
        {
          var requestType = ctx.Input.Types[service.RequestTypeID];
          var responseType = ctx.Input.Types[service.ResponseTypeID];

          o.WriteLine();
          WriteRequestType(requestType, o, service, ctx);
          handledTypes.Add(service.RequestTypeID);
          o.WriteLine();

          // Write response
          if (responseType.Assembly == service.Assembly && handledTypes.Add(service.ResponseTypeID))
          {
            WriteResponseType(service.ResponseTypeID, responseType, o, ctx);
          }
        }

        foreach (var type in i.Types)
        {
          if (handledTypes.Contains(type.Key) || type.Value.ParentType != null) continue;
          o.WriteLine();
          WriteType(type.Key, type.Value, o, ctx);
        }
      });
      sb.WriteLine("}");

      await ctx.Writer.WriteFileAsync($"{actualNamespace}.cs", sb.ToString());
    }
  }

  private void WriteType(string id, TypeSpecification spec, IndentedStringBuilder sb, OutputContext<DotNetOptions> ctx)
  {
    // These types have special handling
    if (ctx.Options.UseNativeDayOfWeek && id == ApiSpecConsts.WellKnown.DayOfWeek) return;

    if (spec.EnumIsFlag.HasValue)
    {
      if (spec.EnumIsFlag.Value) sb.WriteLine("[Flags]");
      sb.WriteLine($"public enum {spec.TypeName}");
      sb.WriteLine("{");
      sb.WriteIndented(o =>
      {
        foreach (var (name, value) in spec.EnumValues)
        {
          var values = value.Value == 0L && !value.Extends.Any() ? new[] { "0" } : value.Extends.Concat(new[] { value.Value.ToString() });
          o.WriteLine($"{name} = {string.Join(" | ", values)},");
        }
      });
      sb.WriteLine("}");
    }
    else
    {
      var typeName = spec.TypeName;
      if (spec.TypeArguments.Any())
      {
        typeName = typeName.Split('`')[0] + $"<{string.Join(',', spec.TypeArguments.Select(x => x[1..]))}>";
      }

      sb.WriteLine($"public class {typeName}");
      sb.WriteLine("{");
      var usage = (spec.Usage.Request ? TypeContext.Request : TypeContext.None) | (spec.Usage.Response ? TypeContext.Response : TypeContext.None);
      sb.WriteIndented(o => { WriteTypeBody(id, spec, o, usage, ctx); });
      sb.WriteLine("}");
    }
  }

  private void WriteResponseType(string id, TypeSpecification spec, IndentedStringBuilder sb, OutputContext<DotNetOptions> ctx)
  {
    sb.WriteLine($"public class {spec.TypeName} : EVA.SDK.Core.IResponseMessage");
    sb.WriteLine("{");
    sb.WriteIndented(o => { WriteTypeBody(id, spec, o, TypeContext.Response, ctx); });
    sb.WriteLine("}");
  }

  private void WriteRequestType(TypeSpecification requestType, IndentedStringBuilder o, ServiceModel service, OutputContext<DotNetOptions> ctx)
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
    o.WriteLine($"[EVA.SDK.Core.RequestMessage(\"{service.Path}\")]");
    o.WriteLine($"public class {service.Name} : EVA.SDK.Core.IResponseType<{GetFullName(service.ResponseTypeID, ctx.Input)}>");
    o.WriteLine("{");

    using (o.Indentation)
    {
      WriteTypeBody(service.RequestTypeID, requestType, o, TypeContext.Request, ctx);
    }

    o.WriteLine("}");
  }

  private void WriteTypeBody(string id, TypeSpecification spec, IndentedStringBuilder o, TypeContext context, OutputContext<DotNetOptions> ctx)
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

      var shouldOutputCustomIDs = prop is { Value.DataModelInformation.SupportsBackendID: true } && context.HasFlag(TypeContext.Request);
      var fullPropName = GetFullName(prop.Value.Type,  context, shouldOutputCustomIDs, ctx);
      fullPropName = prop.Value.Skippable ? $"EVA.SDK.Core.Maybe<{fullPropName}>" : fullPropName;

      // This is very hacky, but here we go!!!!
      if (ctx.Options.EnableCustomIdMode && shouldOutputCustomIDs && fullPropName is "EVA.SDK.Core.Maybe<EVA.SDK.Core.ModelID>" or "EVA.SDK.Core.Maybe<EVA.SDK.Core.ModelID?>")
      {
        fullPropName = "EVA.SDK.Core.MaybeModelID";
      }

      o.WriteLine($"public {fullPropName} {prop.Key} {{ get; set; }}");
    }

    foreach (var ts in ctx.Input.Types.Where(t => t.Value.ParentType == id))
    {
      WriteType(ts.Key, ts.Value, o, ctx);
    }
  }

  private string GetFullName(TypeReference r, TypeContext context, bool returnCustomID, OutputContext<DotNetOptions> ctx)
  {
    var n = r.Nullable ? "?" : string.Empty;

    if (r.Name == ApiSpecConsts.Guid) return $"System.Guid{n}";
    if (r.Name == ApiSpecConsts.String) return $"string{n}";
    if (r.Name == ApiSpecConsts.Int32) return $"int{n}";
    if (r.Name == ApiSpecConsts.Int16) return $"short{n}";
    if (r.Name == ApiSpecConsts.Bool) return $"bool{n}";
    if (r.Name == ApiSpecConsts.Binary) return $"byte[]{n}";
    if (r.Name == ApiSpecConsts.Float128) return $"decimal{n}";
    if (r.Name == ApiSpecConsts.Float64) return $"double{n}";
    if (r.Name == ApiSpecConsts.Float32) return $"float{n}";

    if (r.Name == ApiSpecConsts.Int64)
    {
      if (returnCustomID && ctx.Options.EnableCustomIdMode) return $"EVA.SDK.Core.ModelID{n}";
      return $"long{n}";
    }

    if (returnCustomID && r.Name == ApiSpecConsts.Specials.Array && r.Arguments[0] is {Name: ApiSpecConsts.Int64, Nullable: false} && ctx.Options.EnableCustomIdMode)
    {
      return $"EVA.SDK.Core.ModelIDList{n}";
    }

    if (r.Name == ApiSpecConsts.Specials.Array)
    {
      var type = (context & TypeContext.Request) != 0 ? "IEnumerable" : "List";
      return $"{type}<{GetFullName(r.Arguments[0], context, returnCustomID, ctx)}>{n}";
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

    if (r.Name == ApiSpecConsts.Specials.Map) return $"IDictionary<{GetFullName(r.Arguments[0], context, false, ctx)},{GetFullName(r.Arguments[1], context, returnCustomID, ctx)}>{n}";
    if (r.Name == ApiSpecConsts.Object) return $"JObject{n}";
    if (r.Name == ApiSpecConsts.WellKnown.IProductSearchItem) return $"JObject{n}";
    if (r.Name == ApiSpecConsts.Any) return (context & TypeContext.Request) != 0 ? $"object{n}" : $"JToken{n}";
    if (ctx.Options.UseNativeDayOfWeek && r.Name == ApiSpecConsts.WellKnown.DayOfWeek) return $"DayOfWeek{n}";

    if (r.Name.StartsWith("EVA."))
    {
      var name = r.Name;

      if (!r.Arguments.Any()) return GetFullName(name, ctx.Input) + n;

      var joinedArguments = string.Join(',', r.Arguments.Select(a => GetFullName(a, context, false, ctx)));
      return $"{FixNamespace(name[..name.IndexOf('`')])}<{joinedArguments}>{n}";
    }

    if (r.Name.StartsWith("_"))
    {
      return r.Name[1..] + n;
    }

    ctx.Logger.LogWarning("Type cannot be handled by this output: {Type}, outputting as \"object\"", r.Name);
    return "object";
  }

  private static string GetFullName(string id, ApiDefinitionModel input)
  {
    var type = input.Types[id];
    if (type.ParentType != null)
    {
      return GetFullName(type.ParentType, input) + "." + type.TypeName;
    }

    return $"{FixNamespace(type.Assembly)}.{type.TypeName}";
  }

  private static string FixNamespace(string ns)
  {
    return ns.Replace("EVA.", "EVA.SDK.");
  }
}