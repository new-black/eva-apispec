using System.Text.Json;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;

internal class SwiftOutput : IOutput<SwiftOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedRemoves => new[] { "options", "event-exports", "errors" };

  public async Task Write(OutputContext<SwiftOptions> ctx)
  {
    var (input, _, writer, _) = ctx;

    // Write all services
    foreach (var service in input.Services)
    {
      // Vars
      var assembly = FolderFromAssembly(service.Assembly);
      var filename = "Svc" + FileNameFromType(input.Types[service.RequestTypeID]);
      var reqName = GetTypeName(service.RequestTypeID, input);
      var resName = GetTypeName(service.ResponseTypeID, input);

      var output = new IndentedStringBuilder(4);

      output.WriteLine("import Foundation");
      output.WriteLine();
      output.WriteLine($"public final class {filename}: EvaService<{reqName}, {resName}> {{");
      using (output.Indentation)
      {
        output.WriteLine("public init(endpoint: EvaEndpoint) {");
        using (output.Indentation)
        {
          output.WriteLine("super.init(");
          using (output.Indentation)
          {
            output.WriteLine("endpoint: endpoint,");
            output.WriteLine($"name: \"{assembly}:{service.Name}\",");
            output.WriteLine($"path: \"{service.Path}\"");
          }
          output.WriteLine(")");
        }
        output.WriteLine("}");
      }
      output.WriteLine("}");
      output.Write(string.Empty);


      await writer.WriteFileAsync($"{assembly}/{filename}.swift", output.ToString());
    }

    // Write all types
    foreach (var (id, type) in input.Types)
    {
      // These are nested types, those will be written by their parents.
      if (type.ParentType != null) continue;

      var assembly = FolderFromAssembly(type.Assembly);
      var filename = FileNameFromType(type);
      var typename = GetTypeName(id, input);

      var output = new IndentedStringBuilder(2);

      output.WriteLine("import Foundation");
      output.WriteLine();

      WriteType(type, id, typename, output, ctx);

      await writer.WriteFileAsync($"{assembly}/{filename}.swift", output.ToString());
    }
  }

  private void WriteType(TypeSpecification type, string id, string typename, IndentedStringBuilder output, OutputContext<SwiftOptions> ctx)
  {
    if (type.Description != null)
    {
      WriteComment(type.Description, output);
    }

    var service = ctx.Input.Services.FirstOrDefault(s => s.RequestTypeID == id);
    if (service is { Deprecated: { } deprecationInfo })
    {
      output.WriteLine($"@available(*, deprecated, message: \"{EscapeString(deprecationInfo.Comment ?? string.Empty)}\")");
    }

    if (type.EnumIsFlag.HasValue)
    {
      if (type.EnumIsFlag.Value)
      {
        WriteFlagsEnum(type, typename, output);
      }
      else
      {
        WriteNonFlagsEnum(type, typename, output);
      }

      return;
    }

    var extends = type.Extends == null ? "Codable" : GetTypeName(type.Extends, ctx);

    var typeArguments = string.Empty;
    if (type.TypeArguments.Any())
    {
      typeArguments = string.Join(", ", type.TypeArguments.Select(x => $"{x[1..]} : Codable"));
      typeArguments = $"<{typeArguments}>";
    }

    output.WriteLine($"public struct {typename}{typeArguments}: {extends} {{");
    output.WriteLine();

    output.WriteIndented(output =>
    {
      output.WriteLine("public init(");

      output.WriteIndented(o =>
      {
        var list = type.Properties.ToList();
        for (var i = 0; i < list.Count; i++)
        {
          var prop = list[i];
          var propDefault = GetPropDefault(prop.Value.Type);
          o.WriteLine(
            $"{prop.Key}: {GetPropTypeName(prop.Value.Type, id, ctx)}{(string.IsNullOrEmpty(propDefault) ? string.Empty : $" = {propDefault}")}{(i == list.Count - 1 ? string.Empty : ",")}");
        }
      });

      output.WriteLine(") {");

      output.WriteIndented(o =>
      {
        foreach (var prop in type.Properties.Keys)
        {
          o.WriteLine($"self.{prop} = {prop}");
        }
      });

      output.WriteLine("}");
      output.WriteLine();

      foreach (var (propName, prop) in type.Properties)
      {
        var propType = GetPropTypeName(prop.Type, id, ctx);
        if (prop.Description != null) WriteComment(prop.Description, output);
        if (prop.Deprecated != null)
        {
          output.WriteLine($"@available(*, deprecated, message: \"{EscapeString(prop.Deprecated.Comment ?? string.Empty)}\")");
        }

        var safePropertyName = new[] { "Type" }.Contains(propName) ? $"`{propName}`" : propName;
        output.WriteLine($"public var {safePropertyName} : {propType}");
        output.WriteLine();
      }
    });

    // Write the nested types
    foreach (var (nestedID, nestedType) in ctx.Input.Types.Where(kv => kv.Value.ParentType == id))
    {
      output.WriteLine();

      output.WriteIndented(o => { WriteType(nestedType, nestedID, nestedType.TypeName, o, ctx); });
    }

    output.WriteLine("}");
  }

  private static void WriteNonFlagsEnum(TypeSpecification type, string typename, IndentedStringBuilder output)
  {
    output.WriteLine($"public enum {typename}: Int, Codable {{");

    output.WriteIndented(o =>
    {
      foreach (var (name, value) in type.EnumValues.OrderBy(v => v.Value.Value))
      {
        o.WriteLine($"case {name} = {value.Value}");
      }
    });

    output.WriteLine("}");
  }

  private static void WriteFlagsEnum(TypeSpecification type, string typename, IndentedStringBuilder output)
  {
    output.WriteLine($"public struct {typename}: OptionSet, Codable {{");

    output.WriteIndented(o =>
    {
      o.WriteLine("public let rawValue: Int");
      o.WriteLine();
      o.WriteLine("public init(rawValue: Int) {");
      o.WriteIndented(o => { o.WriteLine("self.rawValue = rawValue"); });
      o.WriteLine("}");
      o.WriteLine();

      var enumValuesWithTotal = type.EnumValues
        .Select(kv => (kv.Key, Value: kv.Value.Value + kv.Value.Extends.Select(e => type.EnumValues[e].Value).Sum()))
        .OrderBy(v => v.Value);

      foreach (var (name, value) in enumValuesWithTotal)
      {
        if (value == 0) continue;
        o.WriteLine($"public static let {name} = {typename}(rawValue: {value})");
      }
    });

    output.WriteLine("}");
  }

  private string GetPropTypeName(TypeReference typeReference, string? typeContext, OutputContext<SwiftOptions> ctx)
  {
    if (typeReference.Name == typeContext)
    {
      // IndirectOptional time!
      if (typeReference.Nullable)
      {
        var nestedReference = typeReference.CloneAsNotNull();
        return $"IndirectOptional<{GetTypeName(nestedReference, ctx)}>?";
      }
      else
      {
        return $"IndirectOptional<{GetTypeName(typeReference, ctx)}>";
      }
    }

    return GetTypeName(typeReference, ctx);
  }

  private static string GetPropDefault(TypeReference typeReference)
  {
    return typeReference switch
    {
      { Name: ApiSpecConsts.String, Nullable: false } => "\"\"",
      { Nullable: true } => "nil",
      _ => string.Empty
    };
  }

  private string GetTypeName(TypeReference typeReference, OutputContext<SwiftOptions> ctx)
  {
    var n = typeReference.Nullable ? "?" : string.Empty;

    if (typeReference is { Name: ApiSpecConsts.String or ApiSpecConsts.Duration or ApiSpecConsts.Binary }) return $"String{n}";
    if (typeReference is { Name: ApiSpecConsts.Int16 or ApiSpecConsts.Int32 or ApiSpecConsts.Int64 }) return $"Int{n}";
    if (typeReference is { Name: ApiSpecConsts.Float128 }) return $"Decimal{n}";
    if (typeReference is { Name: ApiSpecConsts.Float64 }) return $"Double{n}";
    if (typeReference is { Name: ApiSpecConsts.Date }) return $"Date{n}";
    if (typeReference is { Name: ApiSpecConsts.Bool }) return $"Bool{n}";
    if (typeReference is { Name: ApiSpecConsts.Guid }) return $"UUID{n}";
    if (typeReference is { Name: ApiSpecConsts.Specials.Array, Arguments: { Length: 1 } x }) return $"[{GetTypeName(x[0].CloneAsNotNull(), ctx)}]{n}";
    if (typeReference is { Name: ApiSpecConsts.Any or ApiSpecConsts.Object }) return $"{ctx.Options.AnyCodableName}{n}";
    if (typeReference is { Name: ApiSpecConsts.WellKnown.IProductSearchItem }) return $"[String: {ctx.Options.AnyCodableName}]{n}";
    if (typeReference is { Name: ApiSpecConsts.Specials.Map })
    {
      var ta = typeReference.Arguments[1];
      if(ctx.Options.OptimisticNullability) ta = ta.CloneAsNotNull();
      return $"[String: {GetTypeName(ta, ctx)}]{n}";
    }
    if (typeReference.Name.StartsWith("_")) return $"{typeReference.Name[1..]}{n}";

    if (typeReference.Name.StartsWith("EVA.") && typeReference.Arguments is { Length: > 0 })
    {
      return $"{GetTypeName(typeReference.Name, ctx.Input)}<{string.Join(", ", typeReference.Arguments.Select(a => GetTypeName(ctx.Options.OptimisticNullability ? a.CloneAsNotNull() : a, ctx)))}>{n}";
    }

    if (typeReference.Name.StartsWith("EVA."))
    {
      if (ctx.Input.Types.TryGetValue(typeReference.Name, out var referencedType) && referencedType.ParentType != null)
      {
        return $"{referencedType.TypeName}{n}";
      }
      else
      {
        return $"{GetTypeName(typeReference.Name, ctx.Input)}{n}";
      }
    }

    ctx.Logger.LogWarning("Type cannot be handled by this output: {Type}, outputting as \"object\"", typeReference.Name);
    return $"{ctx.Options.AnyCodableName}{n}";
  }

  private static string GetTypeName(string id, ApiDefinitionModel input)
  {
    var reference = input.Types[id];
    var assembly = reference.Assembly;
    assembly = assembly.Replace(".Services", string.Empty);

    var typeName = reference.TypeName;
    var splittedTypename = typeName.Split('`');
    if (splittedTypename.Length > 1 && int.TryParse(splittedTypename[1], out _))
    {
      typeName = splittedTypename[0];
    }

    return $"{assembly}{typeName}".Replace(".", string.Empty);
  }

  private static string FolderFromAssembly(string assembly)
  {
    if (assembly.StartsWith("EVA.")) assembly = assembly[4..];
    assembly = assembly.Replace(".Services", string.Empty);
    assembly = assembly.Replace(".", "/");

    return assembly;
  }

  private static string FileNameFromType(TypeSpecification type)
  {
    var assembly = type.Assembly;
    if (assembly.StartsWith("EVA.")) assembly = assembly[4..];
    assembly = assembly.Replace(".Services", string.Empty);

    var typeName = type.TypeName;
    var splittedTypename = typeName.Split('`');
    if (splittedTypename.Length > 1 && int.TryParse(splittedTypename[1], out _))
    {
      typeName = splittedTypename[0];
    }

    return $"{assembly}{typeName}".Replace(".", string.Empty);
  }

  private static void WriteComment(string s, IndentedStringBuilder output)
  {
    foreach (var line in s.Split('\n').Select(x => x.Trim('\r')))
    {
      output.WriteLine($"// {line}");
    }
  }

  private static string EscapeString(string comment)
  {
    return comment.Replace("\"", "\\\"");
  }
}