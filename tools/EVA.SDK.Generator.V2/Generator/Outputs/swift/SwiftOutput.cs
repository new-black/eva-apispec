using System.Text.Json;
using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Generator.Outputs.swift;

public class SwiftOutput : IOutput
{
  private readonly SwiftOptions _options;

  public SwiftOutput(SwiftOptions options)
  {
    _options = options;
  }

  public void FixOptions(GenerateOptions options)
  {
    options.EnsureRemove("options");
  }

  public async Task Write(ApiDefinitionModel input, string outputDirectory)
  {
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
      output.WriteLine($"public class {filename}: EvaService<{reqName}, {resName}> {{");
      output.Indent();
      output.WriteLine("public init(endpoint: EvaEndpoint) {");
      output.Indent();
      output.WriteLine("super.init(");
      output.Indent();
      output.WriteLine("endpoint: endpoint,");
      output.WriteLine($"name: \"{assembly}:{service.Name}\",");
      output.WriteLine($"path: \"{service.Path}\"");
      output.Outdent();
      output.WriteLine(")");
      output.Outdent();
      output.WriteLine("}");
      output.Outdent();
      output.WriteLine("}");
      output.Write(string.Empty);


      var filepath = Path.Combine(outputDirectory, $"{assembly}/{filename}.swift");
      if (!Directory.Exists(Path.GetDirectoryName(filepath))) Directory.CreateDirectory(Path.GetDirectoryName(filepath));
      await File.WriteAllTextAsync(filepath, output.ToString());
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

      WriteType(type, id, typename, input, output);


      var filepath = Path.Combine(outputDirectory, $"{assembly}/{filename}.swift");
      if (!Directory.Exists(Path.GetDirectoryName(filepath))) Directory.CreateDirectory(Path.GetDirectoryName(filepath));
      await File.WriteAllTextAsync(filepath, output.ToString());
    }
  }

  private void WriteType(TypeSpecification type, string id, string typename, ApiDefinitionModel input, IndentedStringBuilder output)
  {
    if (type.Description != null)
    {
      WriteComment(type.Description, output);
    }

    var service = input.Services.FirstOrDefault(s => s.RequestTypeID == id);
    if (service is { Deprecated: { } deprecationInfo })
    {
      output.WriteLine($"@available(*, deprecated, message: \"{EscapeString(deprecationInfo.Comment)}\")");
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

    var extends = type.Extends == null ? "Codable" : GetTypeName(type.Extends, input, id);

    var typeArguments = string.Empty;
    if (type.TypeArguments.Any())
    {
      typeArguments = string.Join(", ", type.TypeArguments.Select(x => $"{x[1..]} : Codable"));
      typeArguments = $"<{typeArguments}>";
    }

    output.WriteLine($"public struct {typename}{typeArguments}: {extends} {{");
    output.WriteLine();

    if (type.Properties != null)
    {
      output.Indent();

      output.WriteLine("public init(");

      output.WriteIndentend(o =>
      {
        var list = type.Properties.ToList();
        for (var i = 0; i < list.Count; i++)
        {
          var prop = list[i];
          o.WriteLine($"{prop.Key}: {GetTypeName(prop.Value.Type, input, id)}{(prop.Value.Type.Nullable ? " = nil" : string.Empty)}{(i == list.Count - 1 ? string.Empty : ",")}");
        }
      });

      output.WriteLine(") {");

      output.WriteIndentend(o =>
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
        var propType = GetTypeName(prop.Type, input, id);
        if (prop.Type.Name == id) propType = $"IndirectOptional<{propType}>";
        if (prop.Description != null) WriteComment(prop.Description, output);
        if (prop.Deprecated != null)
        {
          output.WriteLine($"@available(*, deprecated, \"{EscapeString(prop.Deprecated.Comment)}\")");
        }

        var safePropertyName = new[] { "Type" }.Contains(propName) ? $"`{propName}`" : propName;
        output.WriteLine($"public var {safePropertyName} : {propType}");
        output.WriteLine();
      }

      output.Outdent();
    }
    else
    {
      output.WriteLine("public init() {}");
    }

    // Write the nested types
    foreach (var (nestedID, nestedType) in input.Types.Where(kv => kv.Value.ParentType == id))
    {
      output.WriteLine();

      output.WriteIndentend(o => { WriteType(nestedType, nestedID, nestedType.TypeName, input, o); });
    }

    output.WriteLine("}");
  }

  private static void WriteNonFlagsEnum(TypeSpecification type, string typename, IndentedStringBuilder output)
  {
    output.WriteLine($"public enum {typename}: Int, Codable {{");

    output.WriteIndentend(o =>
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

    output.WriteIndentend(o =>
    {
      o.WriteLine("public let rawValue: Int");
      o.WriteLine();
      o.WriteLine("public init(rawValue: Int) {");
      o.WriteIndentend(o => { o.WriteLine("self.rawValue = rawValue"); });
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

  private string GetTypeName(TypeReference typeReference, ApiDefinitionModel input, string? typeContext)
  {
    var n = typeReference.Nullable ? "?" : string.Empty;

    if (typeReference is { Name: "string" or "duration" }) return $"String{n}";
    if (typeReference is { Name: "int16" or "int32" or "int64" }) return $"Int{n}";
    if (typeReference is { Name: "float128" }) return $"Decimal{n}";
    if (typeReference is { Name: "float64" }) return $"Double{n}";
    if (typeReference is { Name: "date" }) return $"Date{n}";
    if (typeReference is { Name: "bool" }) return $"Bool{n}";
    if (typeReference is { Name: "guid" }) return $"UUID{n}";
    if (typeReference is { Name: "array", Arguments: { Length: 1 } x }) return $"[{GetTypeName(x[0], input, typeContext)}]{n}";
    if (typeReference is { Name: "any" or "object" }) return $"{_options.AnyCodeableName}{n}";
    if (typeReference is { Name: "map" }) return $"[String: {GetTypeName(typeReference.Arguments[1], input, typeContext)}]{n}";
    if (typeReference.Name.StartsWith("_")) return $"{typeReference.Name[1..]}{n}";

    if (typeReference.Name.StartsWith("EVA.") && typeReference.Arguments is { Length: > 0 })
    {
      return $"{GetTypeName(typeReference.Name, input)}<{string.Join(", ", typeReference.Arguments.Select(a => GetTypeName(a, input, typeContext)))}>{n}";
    }

    if (typeReference.Name.StartsWith("EVA."))
    {
      if (input.Types.TryGetValue(typeReference.Name, out var referencedType) && referencedType.ParentType != null)
      {
        return $"{referencedType.TypeName}{n}";
      }
      else
      {
        return $"{GetTypeName(typeReference.Name, input)}{n}";
      }
    }

    Console.WriteLine("No idea how to handle: " + JsonSerializer.Serialize(typeReference, IndentedSerializationHelper.Default.TypeReference));

    return "ASDF";
  }

  private string GetTypeName(string id, ApiDefinitionModel input)
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

  private string FolderFromAssembly(string assembly)
  {
    if (assembly.StartsWith("EVA.")) assembly = assembly[4..];
    assembly = assembly.Replace(".Services", string.Empty);
    assembly = assembly.Replace(".", "/");

    return assembly;
  }

  private string FileNameFromType(TypeSpecification type)
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

  private void WriteComment(string s, IndentedStringBuilder output)
  {
    foreach (var line in s.Split('\n').Select(x => x.Trim('\r')))
    {
      output.WriteLine($"// {line}");
    }
  }

  private string EscapeString(string comment)
  {
    return comment.Replace("\"", "\\\"");
  }
}