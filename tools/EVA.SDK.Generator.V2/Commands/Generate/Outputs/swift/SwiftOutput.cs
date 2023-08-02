using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;

internal class SwiftOutput : IOutput<SwiftOptions>
{
  private static readonly string[] SafePropertyNames = { "Type" };

  public string? OutputPattern => null;

  public string[] ForcedTransformations => new[] { RemoveEventExports.ID, RemoveDataLakeExports.ID, RemoveErrors.ID, RemoveInheritance.ID };

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

      if (ctx.Options.ServiceFormat == "struct")
      {
        WriteService_Struct(output, filename, reqName, resName, assembly, service);
      }
      else
      {
        WriteService_Class(output, filename, reqName, resName, assembly, service);
      }

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

    // Write mocks
    if (ctx.Options.IncludeMocks)
    {
      var content = ManifestResourceHelpers.GetResource("swift.Resources.Mocks.swift");
      if (content != null)
      {
        if (ctx.Options.AnyCodableName != SwiftOptionsBinder.AnyCodableName.Default)
        {
          content = content.Replace(SwiftOptionsBinder.AnyCodableName.Default, ctx.Options.AnyCodableName);
        }

        await writer.WriteFileAsync("Mocks.swift", content);

        if (ctx.Options.ServiceFormat == "struct")
        {
          await writer.WriteFileAsync("Mocks.struct.swift", ManifestResourceHelpers.GetResource("swift.Resources.Mocks.struct.swift"));
        }
        else
        {
          await writer.WriteFileAsync("Mocks.class.swift", ManifestResourceHelpers.GetResource("swift.Resources.Mocks.class.swift"));
        }
      }
    }
  }

  private static void WriteService_Class(IndentedStringBuilder output, string filename, string reqName, string resName, string assembly, ServiceModel service)
  {
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
  }

  private static void WriteService_Struct(IndentedStringBuilder output, string filename, string reqName, string resName, string assembly, ServiceModel service)
  {
    output.WriteLine("import Foundation");
    output.WriteLine();
    output.WriteLine($"public struct {filename}: EvaService {{");
    using (output.Indentation)
    {
      output.WriteLine($"public typealias Request = {reqName}");
      output.WriteLine($"public typealias Response = {resName}");
      output.WriteLine($"public static let name = \"{assembly}:{service.Name}\"");
      output.WriteLine($"public static let path = \"{service.Path}\"");
    }

    output.WriteLine("}");
    output.Write(string.Empty);
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

    var extends = new List<string> { "Codable, Equatable, Hashable, Sendable" };
    var typeArguments = string.Empty;

    if (type.TypeArguments.Any())
    {
      extends.Clear();
      typeArguments = string.Join(", ", type.TypeArguments.Select(x => $"{x[1..]}"));
      typeArguments = $"<{typeArguments}>";
    }

    // If the property ID exists, mark the struct as Identifiable.
    if (type.Properties.ContainsKey("ID"))
    {
      extends.Add("Identifiable");
    }

    output.WriteLine($"public struct {typename}{typeArguments}{(extends.Any() ? $": {string.Join(", ", extends)}" : "")} {{");
    output.WriteLine();

    using (output.Indentation)
    {
      output.WriteLine("public init(");

      using (output.Indentation)
      {
        var list = type.Properties.ToList();
        for (var i = 0; i < list.Count; i++)
        {
          var prop = list[i];
          var propDefault = GetPropDefault(prop.Value.Type);
          output.WriteLine(
            $"{prop.Key}: {GetPropTypeName(prop.Value, prop.Key, id, ctx)}{(string.IsNullOrEmpty(propDefault) ? string.Empty : $" = {propDefault}")}{(i == list.Count - 1 ? string.Empty : ",")}");
        }
      }

      output.WriteLine(")");
      using (output.BracedIndentation)
      {
        foreach (var prop in type.Properties.Keys)
        {
          output.WriteLine($"self.{prop} = {prop}");
        }
      }

      output.WriteLine();

      foreach (var (propName, prop) in type.Properties)
      {
        // If AllowedValues is defined, encode these values in a type safe enum.
        if (prop.AllowedValues.Any())
        {
          output.WriteLine($"public enum {propName}Values: RawRepresentable, Identifiable, Codable, CaseIterable, Equatable, Hashable, Sendable {{");
          using (output.Indentation)
          {
            foreach (var allowedValue in prop.AllowedValues)
            {
              var formatted = allowedValue.Replace(":", "_");
              output.WriteLine($"case {formatted}");
            }

            output.WriteLine("case undocumented(String)");
            output.WriteLine();
            output.WriteLine("public init?(rawValue: String) {");
            using (output.Indentation)
            {
              output.WriteLine("switch rawValue {");
              foreach (var allowedValue in prop.AllowedValues)
              {
                var formatted = allowedValue.Replace(":", "_");
                output.WriteLine($"case \"{allowedValue}\": self = .{formatted}");
              }

              output.WriteLine("default: self = .undocumented(rawValue)");
              output.WriteLine("}");
            }

            output.WriteLine("}");
            output.WriteLine();
            output.WriteLine("public var id: Self { self }");
            output.WriteLine();
            output.WriteLine("public var rawValue: String {");
            using (output.Indentation)
            {
              output.WriteLine("switch self {");
              foreach (var allowedValue in prop.AllowedValues)
              {
                var formatted = allowedValue.Replace(":", "_");
                output.WriteLine($"case .{formatted}: return \"{allowedValue}\"");
              }

              output.WriteLine("case let .undocumented(string): return string");
              output.WriteLine("}");
            }

            output.WriteLine("}");
            output.WriteLine();
            output.WriteLine($"public static var allCases: [{propName}Values] {{");
            using (output.Indentation)
            {
              output.WriteLine("[");
              using (output.Indentation)
              {
                foreach (var allowedValue in prop.AllowedValues)
                {
                  var formatted = allowedValue.Replace(":", "_");
                  output.WriteLine($".{formatted},");
                }
              }

              output.WriteLine("]");
            }

            output.WriteLine("}");
          }

          output.WriteLine("}");
          output.WriteLine();
        }

        if (prop.Type is { Name: ApiSpecConsts.Specials.Option, Arguments: var options })
        {
          // Make this a struct instead of an enum, because the decoding may succeed for multiple types
          // and this cannot be represented in an enum (with associated values).
          // This method is also used in https://github.com/apple/swift-openapi-generator
          output.WriteLine($"public struct {propName}Payload: Codable, Equatable, Hashable, Sendable {{");
          using (output.Indentation)
          {
            foreach (var option in options)
            {
              var typeName = GetTypeName(option, ctx);
              var name = option.Name.Replace("+", ".").Split(".").Last();
              output.WriteLine($"public var {name}: {typeName}");
            }

            output.WriteLine();
            output.WriteLine("public init(");
            using (output.Indentation)
            {
              var list = options.ToList();
              for (var i = 0; i < list.Count; i++)
              {
                var option = list[i];
                var propDefault = GetPropDefault(option);
                var typeName = GetTypeName(option, ctx);
                var name = option.Name.Replace("+", ".").Split(".").Last();
                output.WriteLine(
                  $"{name}: {typeName}{(string.IsNullOrEmpty(propDefault) ? string.Empty : $" = {propDefault}")}{(i == list.Count - 1 ? string.Empty : ",")}");
              }
            }

            output.WriteLine(")");
            using (output.BracedIndentation)
            {
              foreach (var option in options)
              {
                var name = option.Name.Replace("+", ".").Split(".").Last();
                output.WriteLine($"self.{name} = {name}");
              }
            }

            output.WriteLine();
            output.WriteLine("public init(from decoder: Decoder) throws");
            using (output.BracedIndentation)
            {
              foreach (var option in options)
              {
                var name = option.Name.Replace("+", ".").Split(".").Last();
                output.WriteLine($"{name} = try? .init(from: decoder)");
              }

              output.WriteLine("try DecodingError.verifyAtLeastOneSchemaIsNotNil(");
              using (output.Indentation)
              {
                output.WriteLine("[");
                using (output.Indentation)
                {
                  foreach (var option in options)
                  {
                    var name = option.Name.Replace("+", ".").Split(".").Last();
                    output.WriteLine($"{name},");
                  }
                }

                output.WriteLine("],");
                output.WriteLine("type: Self.self,");
                output.WriteLine("codingPath: decoder.codingPath");
              }

              output.WriteLine(")");
            }

            output.WriteLine();
            output.WriteLine("public func encode(to encoder: Encoder) throws");
            using (output.BracedIndentation)
            {
              foreach (var option in options)
              {
                var name = option.Name.Replace("+", ".").Split(".").Last();
                output.WriteLine($"try {name}?.encode(to: encoder)");
              }
            }
          }

          output.WriteLine("}");
          output.WriteLine();
        }
      }

      // Identifiable requirement
      if (type.Properties.TryGetValue("ID", out var idProperty))
      {
        output.WriteLine($"public var id: {GetPropTypeName(idProperty, "ID", id, ctx)} {{ self.ID }}");
        output.WriteLine();
      }

      foreach (var (propName, prop) in type.Properties)
      {
        var propType = GetPropTypeName(prop, propName, id, ctx);
        if (prop.Description != null) WriteComment(prop.Description, output);
        if (prop.Deprecated != null)
        {
          output.WriteLine($"@available(*, deprecated, message: \"{EscapeString(prop.Deprecated.Comment ?? string.Empty)}\")");
        }

        var safePropertyName = SafePropertyNames.Contains(propName) ? $"`{propName}`" : propName;
        output.WriteLine($"public var {safePropertyName} : {propType}");
        output.WriteLine();
      }
    }

    // Write the nested types
    foreach (var (nestedID, nestedType) in ctx.Input.Types.Where(kv => kv.Value.ParentType == id))
    {
      output.WriteLine();

      using (output.Indentation)
      {
        WriteType(nestedType, nestedID, nestedType.TypeName, output, ctx);
      }
    }

    if (!type.TypeArguments.Any() && type.Properties.Any())
    {
      WriteDecodeInit(type, output, id, ctx);
    }

    output.WriteLine("}");

    // Extension functions for types with argument.
    if (type.TypeArguments.Any())
    {
      output.WriteLine();
      string[] protocols = { "Codable", "Equatable", "Hashable", "Sendable" };
      foreach (var protocol in protocols)
      {
        typeArguments = string.Join(", ", type.TypeArguments.Select(x => $"{x[1..]}: {protocol}"));
        if (protocol == "Codable")
        {
          output.WriteLine($"extension {typename}: {protocol} where {typeArguments} {{");
          WriteDecodeInit(type, output, id, ctx);
          output.WriteLine("}");
        }
        else
        {
          output.WriteLine($"extension {typename}: {protocol} where {typeArguments} {{}}");
        }
      }
    }
  }

  private static void WriteDecodeInit(TypeSpecification type, IndentedStringBuilder output, string? typeContext, OutputContext<SwiftOptions> ctx)
  {
    // Custom decoder
    using (output.Indentation)
    {
      output.WriteLine("public init(from decoder: Decoder) throws");
      using (output.BracedIndentation)
      {
        output.WriteLine("let container = try decoder.container(keyedBy: CodingKeys.self)");

        foreach (var (key, value) in type.Properties)
        {
          var typeName = GetPropTypeName(value, key, typeContext, ctx);
          var typeNameNotNullable = GetPropTypeName(value, key, typeContext, ctx, true);

          // Check if we have a conflicting property defined that will "claim" our typename
          // This is usually the case for props name Date or Data
          var typePrefix = string.Empty;
          if (type.Properties.ContainsKey(typeNameNotNullable))
          {
            typePrefix = "Foundation.";
          }

          if (value.Type.Nullable)
          {
            output.WriteLine($"self.{key} = try? container.decodeIfPresent({typePrefix}{typeNameNotNullable}.self, forKey: .{key})");
          }
          else
          {
            output.WriteLine($"self.{key} = try container.decode({typePrefix}{typeName}.self, forKey: .{key})");
          }
        }
      }
    }
  }

  private static void WriteNonFlagsEnum(TypeSpecification type, string typename, IndentedStringBuilder output)
  {
    output.WriteLine($"public enum {typename}: Int, Identifiable, Codable, Equatable, Hashable, Sendable {{");
    using (output.Indentation)
    {
      foreach (var (name, value) in type.EnumValues.OrderBy(v => v.Value.Value))
      {
        var safeName = name == "Type" ? "`Type`" : name;
        output.WriteLine($"case {safeName} = {value.Value}");
      }

      output.WriteLine();
      output.WriteLine("public var id: Self { self }");
    }

    output.WriteLine("}");
  }

  private static void WriteFlagsEnum(TypeSpecification type, string typename, IndentedStringBuilder output)
  {
    output.WriteLine($"public struct {typename}: OptionSet, Codable, Hashable, Sendable {{");
    using (output.Indentation)
    {
      output.WriteLine("public let rawValue: Int");
      output.WriteLine();
      output.WriteLine("public init(rawValue: Int) {");
      using (output.Indentation)
      {
        output.WriteLine("self.rawValue = rawValue");
      }

      output.WriteLine("}");
      output.WriteLine();

      foreach (var (name, value) in type.EnumValues.ToTotals().OrderBy(v => v.Value))
      {
        if (value == 0) continue;
        output.WriteLine($"public static let {name} = {typename}(rawValue: {value})");
      }
    }

    output.WriteLine("}");
  }

  private static string GetPropTypeName(PropertySpecification ps, string name, string? typeContext, OutputContext<SwiftOptions> ctx, bool forceNotNullable = false)
  {
    var typeReference = ps.Type;
    var n = typeReference.Nullable && !forceNotNullable ? "?" : string.Empty;
    if (ps.AllowedValues.Any()) return $"{name}Values{n}";
    if (typeReference is { Name: ApiSpecConsts.Specials.Option }) return $"{name}Payload{n}";

    if (typeReference.Name == typeContext)
    {
      // IndirectOptional time!
      if (typeReference.Nullable && !forceNotNullable)
      {
        var nestedReference = typeReference.CloneAsNotNull();
        return $"IndirectOptional<{GetTypeName(nestedReference, ctx, forceNotNullable)}>?";
      }
      else
      {
        return $"IndirectOptional<{GetTypeName(typeReference, ctx, forceNotNullable)}>";
      }
    }

    return GetTypeName(typeReference, ctx, forceNotNullable);
  }

  private static string GetPropDefault(TypeReference typeReference)
  {
    return typeReference.Nullable ? "nil" : string.Empty;
  }

  private static string GetTypeName(TypeReference typeReference, OutputContext<SwiftOptions> ctx, bool forceNotNullable = false)
  {
    var n = typeReference.Nullable && !forceNotNullable ? "?" : string.Empty;

    if (typeReference is { Name: ApiSpecConsts.String or ApiSpecConsts.Duration }) return $"String{n}";
    if (typeReference is { Name: ApiSpecConsts.Binary }) return $"Data{n}";
    if (typeReference is { Name: ApiSpecConsts.ID }) return $"Int{n}";
    if (typeReference is { Name: ApiSpecConsts.Int16 or ApiSpecConsts.Int32 or ApiSpecConsts.Int64 }) return $"Int{n}";
    if (typeReference is { Name: ApiSpecConsts.Float128 }) return $"Decimal{n}";
    if (typeReference is { Name: ApiSpecConsts.Float64 }) return $"Double{n}";
    if (typeReference is { Name: ApiSpecConsts.Date }) return $"Date{n}";
    if (typeReference is { Name: ApiSpecConsts.Bool }) return $"Bool{n}";
    if (typeReference is { Name: ApiSpecConsts.Guid }) return $"UUID{n}";
    if (typeReference is { Name: ApiSpecConsts.Specials.Array, Arguments: { Length: 1 } x })
    {
      var element = x[0];
      if (ctx.Options.OptimisticNullability) element = element.CloneAsNotNull();
      return $"[{GetTypeName(element, ctx)}]{n}";
    }

    if (typeReference is { Name: ApiSpecConsts.Any }) return $"{ctx.Options.AnyCodableName}{n}";
    if (typeReference is { Name: ApiSpecConsts.WellKnown.IProductSearchItem or ApiSpecConsts.Object }) return $"[String: {ctx.Options.AnyCodableName}]{n}";
    if (typeReference is { Name: ApiSpecConsts.Specials.Map })
    {
      var ta0 = typeReference.Arguments[0];
      var ta1 = typeReference.Arguments[1];
      return $"[{GetTypeName(ta0, ctx, true)}: {GetTypeName(ta1, ctx, true)}]{n}";
    }

    if (typeReference.Name.StartsWith("_")) return $"{typeReference.Name[1..]}{n}";

    if (typeReference.Name.StartsWith("EVA.") && typeReference.Arguments is { Length: > 0 })
    {
      return $"{GetTypeName(typeReference.Name, ctx.Input)}<{string.Join(", ", typeReference.Arguments.Select(a => GetTypeName(ctx.Options.OptimisticNullability ? a.CloneAsNotNull() : a, ctx)))}>{n}";
    }

    if (typeReference.Name.StartsWith("EVA."))
    {
      var suffix = string.Empty;
      var idName = typeReference.Name;

      while (ctx.Input.Types.TryGetValue(idName, out var referencedType) && referencedType.ParentType != null)
      {
        suffix = $".{referencedType.TypeName}{suffix}";
        idName = referencedType.ParentType;
      }

      return $"{GetTypeName(idName, ctx.Input)}{suffix}{n}";
    }

    ctx.Logger.LogWarning("Type cannot be handled by this output: {Type}, outputting as \"any\"", typeReference.Name);
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