using System.Reflection;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.dotnet;

public class DotNetOutput : IOutput
{
  public DotNetOutput(DotNetOptions options)
  {
  }

  public void FixOptions(GenerateOptions options)
  {
    options.EnsureRemove("options");
    options.EnsureRemove("event-exports");
  }

  private void WriteErrors(Dictionary<string, ErrorSpecification> errors, IndentedStringBuilder o)
  {
    var nonPrefixed = new Dictionary<string, ErrorSpecification>();
    var prefixed = new Dictionary<string, Dictionary<string, ErrorSpecification>>();

    // Select into batches
    foreach (var (prefix, errorSpec) in errors)
    {
      var idx = prefix.IndexOf(':');
      if (idx == -1)
      {
        nonPrefixed.Add(prefix, errorSpec);
      }
      else
      {
        var p = prefix[..idx];
        var q = prefixed[p] = prefixed.GetValueOrDefault(p) ?? new();
        q[prefix[(idx + 1)..]] = errorSpec;
      }
    }

    // Write top-level
    foreach (var (name, errorSpec) in nonPrefixed)
    {
      var message = errorSpec.Message;
      if (errorSpec.Arguments.Any())
      {
        message = string.Format(message, errorSpec.Arguments.Select((a, i) => $"{{{a.Name ?? i.ToString()}:{a.Type.Name}}}").ToArray());
      }

      o.WriteLines(message, "/// ");
      o.WriteLine($"public const string {name} = \"{errorSpec.Name}\";");
    }

    // Write subtypes
    o.WriteLine();
    foreach (var (prefix, subErrors) in prefixed)
    {
      o.WriteLine($"public static class {prefix}");
      o.WriteLine("{");
      o.WriteIndentend(o2 => WriteErrors(subErrors, o2));
      o.WriteLine("}");
    }
  }

  public async Task Write(ApiDefinitionModel input, string outputDirectory)
  {
    var servicesGroupedByAssembly = input.Services.GroupBy(s => s.Assembly);
    var typesGroupedByAssembly = input.Types.GroupBy(x => x.Value.Assembly).ToDictionary(x => x.Key, x => x.ToList());
    var handledAssemblies = new HashSet<string>();

    foreach (var services in servicesGroupedByAssembly)
    {
      handledAssemblies.Add(services.Key);
      var handledTypes = new HashSet<string>();

      var actualNamespace = FixNamespace(services.Key);
      var sb = new IndentedStringBuilder(2);

      sb.WriteLine("#nullable enable");
      sb.WriteLine();
      sb.WriteLine("using System;");
      sb.WriteLine("using System.Collections.Generic;");
      sb.WriteLine("using Newtonsoft.Json.Linq;");
      sb.WriteLine("using System.ComponentModel;");
      sb.WriteLine();
      sb.WriteLine($"namespace {actualNamespace}");
      sb.WriteLine("{");
      sb.WriteIndentend(o =>
      {
        if (actualNamespace == "EVA.SDK.Core")
        {
          o.WriteManifestResourceStream("dotnet.Resources.EVA.SDK.Core.cs");
        }

        var errors = input.Errors.Where(e => e.Assembly == services.Key).ToDictionary(x => x.Name);

        if (errors.Any())
        {
          o.WriteLine("public static class Errors");
          o.WriteLine("{");
          o.WriteIndentend(o2 => WriteErrors(errors, o2));
          o.WriteLine("}");
        }

        foreach (var service in services)
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

        foreach (var type in typesGroupedByAssembly.GetValueOrDefault(services.Key, new List<KeyValuePair<string, TypeSpecification>>()))
        {
          if (handledTypes.Contains(type.Key) || type.Value.ParentType != null) continue;
          o.WriteLine();
          WriteType(type.Key, type.Value, o, input);
        }
      });
      sb.WriteLine("}");

      await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{actualNamespace}.cs"), sb.ToString());
    }

    foreach (var types in input.Types.Values.GroupBy(t => t.Assembly).Where(g => !handledAssemblies.Contains(g.Key)))
    {
      var actualNamespace = FixNamespace(types.Key);

      var sb = new IndentedStringBuilder(2);

      sb.WriteLine("#nullable enable");
      sb.WriteLine();
      sb.WriteLine("}");

      await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{actualNamespace}.cs"), sb.ToString());
    }
  }

  private void WriteType(string id, TypeSpecification spec, IndentedStringBuilder sb, ApiDefinitionModel input)
  {
    // These types have special handling
    if (id == "EVA.Core.DayOfWeek") return;

    if (spec.EnumIsFlag.HasValue)
    {
      if (spec.EnumIsFlag.Value) sb.WriteLine("[Flags]");
      sb.WriteLine($"public enum {spec.TypeName}");
      sb.WriteLine("{");
      sb.WriteIndentend(o =>
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
      sb.WriteIndentend(o => { WriteTypeBody(id, spec, o, input, usage); });
      sb.WriteLine("}");
    }
  }

  private void WriteResponseType(string id, TypeSpecification spec, IndentedStringBuilder sb, ApiDefinitionModel input)
  {
    sb.WriteLine($"public class {spec.TypeName} : EVA.SDK.Core.IResponseMessage");
    sb.WriteLine("{");
    sb.WriteIndentend(o => { WriteTypeBody(id, spec, o, input, TypeContext.Response); });
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

    o.WriteIndentend(o => { WriteTypeBody(service.RequestTypeID, requestType, o, input, API.Spec.TypeContext.Request); });

    o.WriteLine("}");
  }

  private void WriteTypeBody(string id, TypeSpecification spec, IndentedStringBuilder o, ApiDefinitionModel input, TypeContext context)
  {
    if (spec.Properties != null)
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
          o.WriteLine("/// </remarks>");
        }

        if (prop.Value.Deprecated != null)
        {
          o.WriteLine($"[Obsolete(@\"{prop.Value.Deprecated.Comment?.Replace("\"", "\"\"")}\")]");
        }

        o.WriteLine($"public {GetFullName(prop.Value.Type, input, context)} {prop.Key} {{ get; set; }}");
      }
    }

    foreach (var ts in input.Types.Where(t => t.Value.ParentType == id))
    {
      WriteType(ts.Key, ts.Value, o, input);
    }
  }

  private string GetFullName(TypeReference r, ApiDefinitionModel input, TypeContext context)
  {
    var n = r.Nullable ? "?" : string.Empty;

    if (r.Name == "guid") return $"System.Guid{n}";
    if (r.Name == "string") return $"string{n}";
    if (r.Name == "int64") return $"long{n}";
    if (r.Name == "int32") return $"int{n}";
    if (r.Name == "int16") return $"short{n}";
    if (r.Name == "bool") return $"bool{n}";
    if (r.Name == "binary") return $"byte[]{n}";
    if (r.Name == "float128") return $"decimal{n}";
    if (r.Name == "float64") return $"double{n}";
    if (r.Name == "float32") return $"float{n}";
    if (r.Name == "array")
    {
      var type = (context & TypeContext.Request) != 0 ? "IEnumerable" : "IList";
      return $"{type}<{GetFullName(r.Arguments[0], input, context)}>{n}";
    }

    if (r.Name == "date") return $"System.DateTime{n}";
    if (r.Name == "duration") return $"TimeSpan{n}";

    // Special case for type IDictionary<string, object?>
    if (r is { Name: "map", Arguments: [ { Name: "string" }, { Name: "any" }] })
    {
      return (context & TypeContext.Request) != 0 ? $"IDictionary<string, JToken>{n}" : $"JObject{n}";
    }
    if (r is { Name: "map", Arguments: [ { Name: "int64" }, { Name: "any" }] })
    {
      return $"IDictionary<long, JToken>{n}";
    }

    if (r.Name == "map") return $"IDictionary<{GetFullName(r.Arguments[0], input, context)},{GetFullName(r.Arguments[1], input, context)}>{n}";
    if (r.Name == "object") return $"JObject{n}";
    if (r.Name == "EVA.Core.Search.IProductSearchItem") return $"JObject{n}";
    if (r.Name == "any") return (context & TypeContext.Request) != 0 ? $"object{n}" : $"JToken{n}";
    if (r.Name == "EVA.Core.DayOfWeek") return $"DayOfWeek{n}";

    if (r.Name.StartsWith("EVA."))
    {
      var name = r.Name;

      if (r.Arguments.Any())
      {
        var joinedArguments = string.Join(',', r.Arguments.Select(a => GetFullName(a, input, context)));
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