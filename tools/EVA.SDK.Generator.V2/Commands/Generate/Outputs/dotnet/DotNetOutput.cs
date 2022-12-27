using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;

public class DotNetOutput : IOutput
{
  [Flags]
  public enum TypeContext
  {
    None = 0,
    Request = 1,
    Response = 2
  }

  private readonly DotNetOptions _options;

  public DotNetOutput(DotNetOptions options)
  {
    _options = options;
  }

  public string OutputPattern => "EVA.*.cs";

  public void FixOptions(GenerateOptions options)
  {
    options.EnsureRemove("options");
    options.EnsureRemove("event-exports");
  }

  private void WriteErrors(ApiDefinitionModelExtensions.PrefixGroupedErrors errors, IndentedStringBuilder o, string name)
  {
    if (!errors.Errors.Any() && !errors.SubErrors.Any()) return;

    o.WriteLine($"public static class {name}");
    o.WriteLine("{");
    o.WriteIndented(o =>
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
    });
    o.WriteLine("}");
  }

  public async Task Write(ApiDefinitionModel input, string outputDirectory)
  {
    var groupedInput = input.GroupByAssembly();

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
      if (_options.JsonSerializer == "newtonsoft") sb.WriteLine("using Newtonsoft.Json.Linq;");
      sb.WriteLine();
      sb.WriteLine($"namespace {actualNamespace}");
      sb.WriteLine("{");
      sb.WriteIndented(o =>
      {
        if (actualNamespace == "EVA.SDK.Core")
        {
          o.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.cs");
          if (_options.JsonSerializer == "newtonsoft")
          {
            o.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.NewtonsoftJson.cs");
          }
        }

        WriteErrors(i.Errors.GroupByPrefix(), o, "Errors");

        foreach (var service in i.Services)
        {
          var requestType = input.Types[service.RequestTypeID];
          var responseType = input.Types[service.ResponseTypeID];

          o.WriteLine();
          WriteRequestType(service.RequestTypeID, requestType, o, input, service);
          handledTypes.Add(service.RequestTypeID);
          o.WriteLine();

          // Write response
          if (responseType.Assembly == service.Assembly && handledTypes.Add(service.ResponseTypeID))
          {
            WriteResponseType(service.ResponseTypeID, responseType, o, input);
          }
        }

        foreach (var type in i.Types)
        {
          if (handledTypes.Contains(type.Key) || type.Value.ParentType != null) continue;
          o.WriteLine();
          WriteType(type.Key, type.Value, o, input);
        }
      });
      sb.WriteLine("}");

      await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{actualNamespace}.cs"), sb.ToString());
    }
  }

  private void WriteType(string id, TypeSpecification spec, IndentedStringBuilder sb, ApiDefinitionModel input)
  {
    // These types have special handling
    if (_options.UseNativeDayOfWeek && id == "EVA.Core.DayOfWeek") return;

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
      sb.WriteIndented(o => { WriteTypeBody(id, spec, o, input, usage); });
      sb.WriteLine("}");
    }
  }

  private void WriteResponseType(string id, TypeSpecification spec, IndentedStringBuilder sb, ApiDefinitionModel input)
  {
    sb.WriteLine($"public class {spec.TypeName} : EVA.SDK.Core.IResponseMessage");
    sb.WriteLine("{");
    sb.WriteIndented(o => { WriteTypeBody(id, spec, o, input, TypeContext.Response); });
    sb.WriteLine("}");
  }

  private void WriteRequestType(string id, TypeSpecification requestType, IndentedStringBuilder o, ApiDefinitionModel input, ServiceModel service)
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
    o.WriteLine($"public class {service.Name} : EVA.SDK.Core.IResponseType<{GetFullName(service.ResponseTypeID, input)}>");
    o.WriteLine("{");

    o.WriteIndented(o => { WriteTypeBody(service.RequestTypeID, requestType, o, input, TypeContext.Request); });

    o.WriteLine("}");
  }

  private void WriteTypeBody(string id, TypeSpecification spec, IndentedStringBuilder o, ApiDefinitionModel input, TypeContext context)
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
      var fullPropName = GetFullName(prop.Value.Type, input, context, shouldOutputCustomIDs);
      fullPropName = prop.Value.Skippable ? $"EVA.SDK.Core.Maybe<{fullPropName}>" : fullPropName;

      // This is very hacky, but here we go!!!!
      if (_options.EnableCustomIdMode && shouldOutputCustomIDs && fullPropName is "EVA.SDK.Core.Maybe<EVA.SDK.Core.ModelID>" or "EVA.SDK.Core.Maybe<EVA.SDK.Core.ModelID?>")
      {
        fullPropName = "EVA.SDK.Core.MaybeModelID";
      }

      o.WriteLine($"public {fullPropName} {prop.Key} {{ get; set; }}");
    }

    foreach (var ts in input.Types.Where(t => t.Value.ParentType == id))
    {
      WriteType(ts.Key, ts.Value, o, input);
    }
  }

  private string GetFullName(TypeReference r, ApiDefinitionModel input, TypeContext context, bool returnCustomID)
  {
    var n = r.Nullable ? "?" : string.Empty;

    if (r.Name == "guid") return $"System.Guid{n}";
    if (r.Name == "string") return $"string{n}";
    if (r.Name == "int32") return $"int{n}";
    if (r.Name == "int16") return $"short{n}";
    if (r.Name == "bool") return $"bool{n}";
    if (r.Name == "binary") return $"byte[]{n}";
    if (r.Name == "float128") return $"decimal{n}";
    if (r.Name == "float64") return $"double{n}";
    if (r.Name == "float32") return $"float{n}";

    if (r.Name == "int64")
    {
      if (returnCustomID && _options.EnableCustomIdMode) return $"EVA.SDK.Core.ModelID{n}";
      return $"long{n}";
    }

    if (returnCustomID && r.Name == "array" && r.Arguments[0] is {Name: "int64", Nullable: false} && _options.EnableCustomIdMode)
    {
      return $"EVA.SDK.Core.ModelIDList{n}";
    }

    if (r.Name == "array")
    {
      var type = (context & TypeContext.Request) != 0 ? "IEnumerable" : "List";
      return $"{type}<{GetFullName(r.Arguments[0], input, context, returnCustomID)}>{n}";
    }

    if (r.Name == "date") return $"System.DateTime{n}";
    if (r.Name == "duration") return $"TimeSpan{n}";

    // Special case for type IDictionary<string, object?>
    //if (r is { Name: "map", Arguments: [ { Name: "string" }, { Name: "any" }] })
    if (r is { Name: "map", Arguments.Length: 2 } && r.Arguments[0].Name == "string" && r.Arguments[1].Name == "any")
    {
      return (context & TypeContext.Request) != 0 ? $"IDictionary<string, JToken>{n}" : $"JObject{n}";
    }

    //if (r is { Name: "map", Arguments: [ { Name: "int64" }, { Name: "any" }] })
    if (r is { Name: "map", Arguments.Length: 2 } && r.Arguments[0].Name == "int64" && r.Arguments[1].Name == "any")
    {
      return $"IDictionary<long, JToken>{n}";
    }

    if (r.Name == "map") return $"IDictionary<{GetFullName(r.Arguments[0], input, context, false)},{GetFullName(r.Arguments[1], input, context, returnCustomID)}>{n}";
    if (r.Name == "object") return $"JObject{n}";
    if (r.Name == "EVA.Core.Search.IProductSearchItem") return $"JObject{n}";
    if (r.Name == "any") return (context & TypeContext.Request) != 0 ? $"object{n}" : $"JToken{n}";
    if (_options.UseNativeDayOfWeek && r.Name == "EVA.Core.DayOfWeek") return $"DayOfWeek{n}";

    if (r.Name.StartsWith("EVA."))
    {
      var name = r.Name;

      if (r.Arguments.Any())
      {
        var joinedArguments = string.Join(',', r.Arguments.Select(a => GetFullName(a, input, context, false)));
        return $"{FixNamespace(name[..name.IndexOf('`')])}<{joinedArguments}>{n}";
      }
      else
      {
        return GetFullName(name, input) + n;
      }
    }

    if (r.Name.StartsWith("_"))
    {
      return r.Name[1..] + n;
    }

    Console.WriteLine($"How to handle: {r.Name}");

    return "UNKNOWN";
  }

  private string GetFullName(string id, ApiDefinitionModel input)
  {
    var type = input.Types[id];
    if (type.ParentType != null)
    {
      return GetFullName(type.ParentType, input) + "." + type.TypeName;
    }

    return $"{FixNamespace(type.Assembly)}.{type.TypeName}";
  }

  private string FixNamespace(string ns)
  {
    return ns.Replace("EVA.", "EVA.SDK.");
  }
}