using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;

internal class SwiftOutput : IOutput<SwiftOptions>
{
  private static readonly string[] SafePropertyNames = ["Type", "Protocol"];

  public string? OutputPattern => null;

  public bool GetForcedTransformations(SwiftOptions _, INamedTransform x) => x is RemoveEventExports or RemoveDataLakeExports or RemoveErrors or RemoveEmptyTypes or RemoveEmptyBaseTypes;

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
      if (resName == "EVACoreGetApplicationConfigurationResponse") resName = "ApplicationConfiguration";

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
      var typename = GetTypeName(id, input);

      var output = new IndentedStringBuilder(2);

      output.WriteLine("import Foundation");
      output.WriteLine();

      WriteType(type, id, typename, output, ctx);

      await writer.WriteFileAsync($"{assembly}/{typename}.swift", output.ToString());
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
      output.WriteLine($"public static let httpMethod = \"{service.Method ?? "POST"}\"");

      var permissions = service.AuthInformation?.RequiredPermissions ?? [];
      if (!permissions.IsEmpty)
      {
        output.WriteLine("public static let requiredPermissions: [EVAPermission] = [");
        using (output.Indentation)
        {
          for (var i = 0; i < permissions.Length; i++)
          {
            var p = permissions[i];
            var userTypes = (int?)p.UserTypes ?? 0;
            var scope = (int?)p.Scope ?? 0;
            var functionality = p.Functionality != null ? $"\"{p.Functionality}\"" : "nil";
            var comma = i < permissions.Length - 1 ? "," : string.Empty;
            output.WriteLine($"EVAPermission(userTypes: {userTypes}, functionality: {functionality}, scope: {scope}){comma}");
          }
        }
        output.WriteLine("]");
      }
    }

    output.WriteLine("}");
    output.Write(string.Empty);
  }

  private void WriteType(TypeSpecification type, string id, string typename, IndentedStringBuilder output, OutputContext<SwiftOptions> ctx, bool isNested = false)
  {
    if (type.Description != null)
    {
      WriteComment(type.Description, output);
    }

    var service = ctx.Input.Services.FirstOrDefault(s => s.RequestTypeID == id);
    if (service is { Deprecated: { } deprecationInfo })
    {
      output.WriteLine($"@available(*, deprecated, message: \"{EscapeString(deprecationInfo.Comment ?? string.Empty)} - Announced in {deprecationInfo.Introduced}, active from {deprecationInfo.Effective}.\")");
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

    // If the property ID exists (directly or via extends), mark the struct as Identifiable.
    if (type.Properties.ContainsKey("ID") || FindIDProperty(type.Extends, ctx.Input) != null)
    {
      extends.Add("Identifiable");
    }

    if (type.Extends != null)
    {
      output.WriteLine("@dynamicMemberLookup");
    }
    output.WriteLine($"public struct {typename}{typeArguments}{(extends.Any() ? $": {string.Join(", ", extends)}" : "")} {{");
    output.WriteLine();

    using (output.Indentation)
    {
      output.WriteLine("public init(");

      using (output.Indentation)
      {
        if (type.Extends != null)
        {
          var parentTypeName = GetTypeName(type.Extends, ctx);
          var parentPropName = GetParentPropName(type.Extends);
          output.WriteLine($"{parentPropName}: {parentTypeName}{(type.Properties.Any() ? "," : "")}");
        }
        var list = type.Properties.ToList();
        for (var i = 0; i < list.Count; i++)
        {
          var prop = list[i];
          var propDefault = GetPropDefault(prop.Value.Type, prop.Value.Skippable || prop.Value.Deprecated != null);
          output.WriteLine(
            $"{prop.Key}: {GetPropTypeName(prop.Value, prop.Key, id, ctx, false, prop.Value.Deprecated != null)}{(string.IsNullOrEmpty(propDefault) ? string.Empty : $" = {propDefault}")}{(i == list.Count - 1 ? string.Empty : ",")}");
        }
      }

      output.WriteLine(")");
      using (output.BracedIndentation)
      {
        if (type.Extends != null)
        {
          var parentPropName = GetParentPropName(type.Extends);
          output.WriteLine($"self.{parentPropName} = {parentPropName}");
        }
        foreach (var prop in type.Properties.Keys)
        {
          output.WriteLine($"self.{prop} = {prop}");
        }
      }
      
      output.WriteLine();

      if (type.Extends != null)
      {
        var flatParentProps = CollectAllPropertiesFlat(type.Extends, ctx.Input);
        if (flatParentProps != null)
        {
          var allFlatProps = flatParentProps
            .Concat(type.Properties.Select(kv => (Name: kv.Key, Spec: kv.Value, TypeCtx: id)))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

          output.WriteLine("public init(");
          using (output.Indentation)
          {
            for (var i = 0; i < allFlatProps.Count; i++)
            {
              var (propName, prop, typeCtx) = allFlatProps[i];
              var propDefault = GetPropDefault(prop.Type, prop.Skippable || prop.Deprecated != null);
              var typePrefix = GetFlatInitTypePrefix(prop, typeCtx, id, ctx.Input);
              output.WriteLine(
                $"{propName}: {GetPropTypeName(prop, propName, typeCtx, ctx, false, prop.Deprecated != null, typePrefix)}{(string.IsNullOrEmpty(propDefault) ? string.Empty : $" = {propDefault}")}{(i == allFlatProps.Count - 1 ? string.Empty : ",")}");
            }
          }

          output.WriteLine(")");
          using (output.BracedIndentation)
          {
            var parentPropName = GetParentPropName(type.Extends);
            var parentCtorCall = BuildParentConstructorCall(type.Extends, ctx);
            var ownPropsArgs = type.Properties.Keys.Select(k => $"{k}: {k}");
            var allArgs = new[] { $"{parentPropName}: {parentCtorCall}" }.Concat(ownPropsArgs);
            output.WriteLine($"self.init({string.Join(", ", allArgs)})");
          }

          output.WriteLine();
        }
      }

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

          // Build a conflict-aware name map: use the short last segment unless two options share it.
          var shortNames = options.Select(o => o.Name.Replace("+", ".").Split(".").Last()).ToList();
          var nameMap = options.Select((o, i) =>
          {
            var shortName = shortNames[i];
            var isConflict = shortNames.Count(n => n == shortName) > 1;
            // For nested types (e.g. "EVA.Core.Devices.ThermalPrinterDeviceTypeData+Base"), the short
            // name is just "Base" which conflicts with sibling nested types. Use only the parent part
            // ("ThermalPrinterDeviceTypeData") rather than concatenating parent+nested ("ThermalPrinterDeviceTypeDataBase").
            return isConflict ? o.Name.Split('.').Last().Split('+').First().Replace("`", string.Empty) : shortName;
          }).ToList();

          // Deduplicate options:
          // 1. Types with no own properties extending the same base decode identically — keep only the first.
          // 2. If a base type and a derived type are both present, the base is always decodable whenever
          //    the derived type is, so the base is redundant — keep only derived types.
          var seenStructuralKeys = new HashSet<string>();
          var dedupedOptions = Enumerable.Range(0, options.Length)
            .Where(i =>
            {
              var o = options[i];
              string key;
              if (ctx.Input.Types.TryGetValue(o.Name, out var optType) && optType.Properties.IsEmpty && optType.Extends != null)
                key = $"extends:{optType.Extends.Name}";
              else
                key = o.Name;
              return seenStructuralKeys.Add(key);
            })
            .Select(i => (Option: options[i], Name: nameMap[i]))
            .ToList();

          var dedupedOptionIds = dedupedOptions.Select(x => x.Option.Name).ToHashSet();
          dedupedOptions = [.. dedupedOptions
            .Where(x => !dedupedOptionIds.Any(otherId => otherId != x.Option.Name && IsInExtendsChain(x.Option.Name, otherId, ctx.Input)))];

          output.WriteLine($"public struct {propName}Payload: Codable, Equatable, Hashable, Sendable {{");
          using (output.Indentation)
          {
            output.WriteLine("public var properties: [String: JSON]");
            foreach (var (option, name) in dedupedOptions)
            {
              var typeName = GetTypeName(option, ctx);
              output.WriteLine($"public var {name}: {typeName}");
            }

            output.WriteLine();
            output.WriteLine("public init(");
            using (output.Indentation)
            {
              output.WriteLine("properties: [String: JSON] = [:]" + ((dedupedOptions.Count == 0) ? "" : ","));
              for (var i = 0; i < dedupedOptions.Count; i++)
              {
                var (option, name) = dedupedOptions[i];
                var propDefault = GetPropDefault(option);
                var typeName = GetTypeName(option, ctx);
                output.WriteLine(
                  $"{name}: {typeName}{(string.IsNullOrEmpty(propDefault) ? string.Empty : $" = {propDefault}")}{(i == dedupedOptions.Count - 1 ? string.Empty : ",")}");
              }
            }

            output.WriteLine(")");
            using (output.BracedIndentation)
            {
              output.WriteLine("self.properties = properties");
              foreach (var (_, name) in dedupedOptions)
              {
                output.WriteLine($"self.{name} = {name}");
              }
            }

            output.WriteLine();
            output.WriteLine("public init(from decoder: Decoder) throws");
            using (output.BracedIndentation)
            {
              output.WriteLine("properties = try .init(from: decoder)");
              foreach (var (_, name) in dedupedOptions)
              {
                output.WriteLine($"{name} = try? .init(from: decoder)");
              }

              output.WriteLine("try DecodingError.verifyAnySchema(");
              using (output.Indentation)
              {
                output.WriteLine("properties,");
                output.WriteLine("type: Self.self,");
                output.WriteLine("codingPath: decoder.codingPath");
              }

              output.WriteLine(")");
            }

            output.WriteLine();
            output.WriteLine("public func encode(to encoder: Encoder) throws");
            using (output.BracedIndentation)
            {
              output.WriteLine($"try properties.encode(to: encoder)");
              foreach (var (_, name) in dedupedOptions)
              {
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
        output.WriteLine($"public var id: {GetPropTypeName(idProperty, "ID", id, ctx, false, idProperty.Deprecated != null)} {{ self.ID }}");
        output.WriteLine();
      }
      else if (type.Extends != null && FindIDProperty(type.Extends, ctx.Input) is { } inheritedIdProperty)
      {
        var parentPropName = GetParentPropName(type.Extends);
        output.WriteLine($"public var id: {GetPropTypeName(inheritedIdProperty, "ID", id, ctx, false, inheritedIdProperty.Deprecated != null)} {{ {parentPropName}.id }}");
        output.WriteLine();
      }

      if (type.Extends != null)
      {
        var parentTypeName = GetTypeName(type.Extends, ctx);
        var parentPropName = GetParentPropName(type.Extends);
        output.WriteLine($"public var {parentPropName}: {parentTypeName}");
        output.WriteLine();
        output.WriteLine($"public subscript<U>(dynamicMember keyPath: KeyPath<{parentTypeName}, U>) -> U {{ {parentPropName}[keyPath: keyPath] }}");
        output.WriteLine();

      }

      foreach (var (propName, prop) in type.Properties)
      {
        var propType = GetPropTypeName(prop, propName, id, ctx, false, prop.Deprecated != null);
        if (prop.Description != null) WriteComment(prop.Description, output);
        if (prop.Deprecated != null)
        {
          output.WriteLine($"@available(*, deprecated, message: \"{EscapeString(prop.Deprecated.Comment ?? string.Empty)} - Announced in {prop.Deprecated.Introduced}, active from {prop.Deprecated.Effective}.\")");
        }

        var safePropertyName = SafePropertyNames.Contains(propName) ? $"`{propName}`" : propName;
        var indirectOptionalPrefix = prop.Type.Name == id ? "@IndirectOptional " : string.Empty;
        output.WriteLine($"{indirectOptionalPrefix}public var {safePropertyName} : {propType}");
        output.WriteLine();
      }
    }

    // Write the nested types
    var nestedTypes = ctx.Input.Types.Where(kv => kv.Value.ParentType == id).ToList();
    foreach (var (nestedID, nestedType) in nestedTypes)
    {
      output.WriteLine();

      using (output.Indentation)
      {
        WriteType(nestedType, nestedID, TypeName(nestedID, nestedType.TypeName, ctx.Input), output, ctx, true);
      }
    }

    if (!type.TypeArguments.Any() && (type.Properties.Any() || type.Extends != null))
    {
      WriteDecodeInit(type, output, id, ctx);
    }

    output.WriteLine("}");

    // Extension functions for types with argument.
    if (!isNested)
    {
      WriteExtensions(type, typename, typeArguments, id, output, ctx);
    }

    foreach (var (nestedID, nestedType) in nestedTypes)
    {
      WriteExtensions(nestedType, $"{typename}.{TypeName(nestedID, nestedType.TypeName, ctx.Input)}", typeArguments, nestedID, output, ctx);
    }
  }

  private string TypeName(string id, string typename, ApiDefinitionModel input)
  {
    return id == "EVA.Core.Stock.GetStockLabelSettingsForLabelResponse+InheritedValue`1" ? GetTypeName(id, input) : typename;
  }

  private void WriteExtensions(TypeSpecification type, string typename, string currentTypeArguments, string id, IndentedStringBuilder output, OutputContext<SwiftOptions> ctx)
  {
    var typeArguments = currentTypeArguments;
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
      if (type.Extends != null && !type.Properties.IsEmpty)
      {
        output.WriteLine("enum CodingKeys: String, CodingKey {");
        using (output.Indentation)
        {
          foreach (var key in type.Properties.Keys)
          {
            var safeKey = SafePropertyNames.Contains(key) ? $"`{key}`" : key;
            output.WriteLine($"case {safeKey}");
          }
        }
        output.WriteLine("}");
        output.WriteLine();
      }

      output.WriteLine("public init(from decoder: Decoder) throws");
      using (output.BracedIndentation)
      {
        if (type.Extends != null)
        {
          var parentTypeName = GetTypeName(type.Extends, ctx);
          var parentPropName = GetParentPropName(type.Extends);
          output.WriteLine($"self.{parentPropName} = try {parentTypeName}(from: decoder)");
        }

        if (type.Properties.Any())
        {
          output.WriteLine("let container = try decoder.container(keyedBy: CodingKeys.self)");
        }

        foreach (var (key, value) in type.Properties)
        {
          var typeName = GetPropTypeName(value, key, typeContext, ctx);
          var typeNameNotNullable = GetPropTypeName(value, key, typeContext, ctx, true);

          var postfix = string.Empty;
          var containsProductDetails = false;
          var isOptional = value.Type.Nullable || value.Deprecated != null || value.Skippable;

          foreach (var t in new string[] { "ProductDetails", "String", "Int" })
          {
            var before = typeNameNotNullable;
            typeNameNotNullable = Regex.Replace(typeNameNotNullable, $@"\b{t}\b",$"{t}Wrapper");
            typeName = Regex.Replace(typeName, $@"\b{t}\b",$"{t}Wrapper");
            typeNameNotNullable = Regex.Replace(typeNameNotNullable, $"{t}Wrapper:",$"{t}:");
            typeName = Regex.Replace(typeName, $"{t}Wrapper:",$"{t}:");

            if (before != typeNameNotNullable)
            {
              if (t == "ProductDetails")
              {
                containsProductDetails = true;
              }
              var property = t == "ProductDetails" ? "productDetails" : "unwrapped";
              postfix = (isOptional ? "?" : "") + "." + property;
            }
          }

          // Check if we have a conflicting property defined that will "claim" our typename
          // This is usually the case for props name Date or Data
          string[] foundationTypes = ["Data", "Date"];
          var typePrefix = (type.Properties.ContainsKey(typeNameNotNullable) && !containsProductDetails || foundationTypes.Contains(typeNameNotNullable)) ? "Foundation." : string.Empty;

          var safeKey = SafePropertyNames.Contains(key) ? $"`{key}`" : key;
          if (isOptional)
          {
            output.WriteLine($"do {{ self.{key} = try container.decodeIfPresent({typePrefix}{typeNameNotNullable}.self, forKey: .{safeKey}){postfix} }} catch {{ decodeLog(error) }}");
          }
          else
          {
            output.WriteLine($"self.{key} = try container.decode({typePrefix}{typeName}.self, forKey: .{safeKey}){postfix}");
          }
        }
      }

      if (type.Extends != null)
      {
        output.WriteLine();
        output.WriteLine("public func encode(to encoder: Encoder) throws");
        using (output.BracedIndentation)
        {
          var parentPropName = GetParentPropName(type.Extends);
          output.WriteLine($"try {parentPropName}.encode(to: encoder)");
          if (type.Properties.Any())
          {
            output.WriteLine("var container = encoder.container(keyedBy: CodingKeys.self)");
            foreach (var (key, value) in type.Properties)
            {
              var safeKey = SafePropertyNames.Contains(key) ? $"`{key}`" : key;
              var isOptional = value.Type.Nullable || value.Deprecated != null || value.Skippable;
              if (isOptional)
              {
                output.WriteLine($"try container.encodeIfPresent(self.{safeKey}, forKey: .{safeKey})");
              }
              else
              {
                output.WriteLine($"try container.encode(self.{safeKey}, forKey: .{safeKey})");
              }
            }
          }
        }
      }
    }
  }

  /// <summary>Returns true if <paramref name="baseId"/> appears anywhere in the extends chain of <paramref name="derivedId"/>.</summary>
  private static bool IsInExtendsChain(string baseId, string derivedId, ApiDefinitionModel input)
  {
    if (!input.Types.TryGetValue(derivedId, out var type)) return false;
    if (type.Extends == null) return false;
    if (type.Extends.Name == baseId) return true;
    return IsInExtendsChain(baseId, type.Extends.Name, input);
  }

  private static PropertySpecification? FindIDProperty(TypeReference? extends, ApiDefinitionModel input)
  {
    if (extends == null || !input.Types.TryGetValue(extends.Name, out var type)) return null;
    if (type.Properties.TryGetValue("ID", out var idProp)) return idProp;
    return FindIDProperty(type.Extends, input);
  }

  private static string GetParentPropName(TypeReference typeRef)
  {
    var name = typeRef.Name.Split('.').Last();
    return name.Replace("`", string.Empty).Replace("+", string.Empty);
  }

  private static List<(string Name, PropertySpecification Spec, string TypeCtx)>? CollectAllPropertiesFlat(
    TypeReference? extends, ApiDefinitionModel input,
    Dictionary<string, TypeReference>? substitutions = null)
  {
    if (extends == null) return [];
    if (!input.Types.TryGetValue(extends.Name, out var type)) return [];

    // Build the substitution map for this level if the type is generic.
    Dictionary<string, TypeReference>? nextSubstitutions = null;
    if (type.TypeArguments.Any())
    {
      var resolvedArgs = extends.Arguments.Select(a => ApplyTypeSubstitutions(a, substitutions)).ToArray();
      if (resolvedArgs.Length != type.TypeArguments.Length) return null;
      nextSubstitutions = type.TypeArguments
        .Zip(resolvedArgs, (param, arg) => (param, arg))
        .ToDictionary(x => x.param, x => x.arg);
    }

    var parentProps = CollectAllPropertiesFlat(type.Extends, input, nextSubstitutions);
    if (parentProps == null) return null;

    var ownProps = type.Properties.Select(kv =>
    {
      var prop = nextSubstitutions != null ? SubstitutePropertyType(kv.Value, nextSubstitutions) : kv.Value;
      return (kv.Key, prop, extends.Name);
    }).ToList();

    return [.. parentProps, .. ownProps];
  }

  private static PropertySpecification SubstitutePropertyType(PropertySpecification prop, Dictionary<string, TypeReference> substitutions)
  {
    return new PropertySpecification(ApplyTypeSubstitutions(prop.Type, substitutions))
    {
      Description = prop.Description,
      Skippable = prop.Skippable,
      Deprecated = prop.Deprecated,
      AllowedValues = prop.AllowedValues,
    };
  }

  private static TypeReference ApplyTypeSubstitutions(TypeReference typeRef, Dictionary<string, TypeReference>? substitutions)
  {
    if (substitutions == null) return typeRef;
    if (substitutions.TryGetValue(typeRef.Name, out var replacement))
      return new TypeReference(replacement.Name, replacement.Arguments, typeRef.Nullable || replacement.Nullable);
    if (typeRef.Arguments.IsEmpty) return typeRef;
    var newArgs = typeRef.Arguments.Select(a => ApplyTypeSubstitutions(a, substitutions)).ToImmutableArray();
    return new TypeReference(typeRef.Name, newArgs, typeRef.Nullable);
  }

  private static string BuildParentConstructorCall(TypeReference typeRef, OutputContext<SwiftOptions> ctx)
  {
    var typeName = GetTypeName(typeRef, ctx, true);

    if (!ctx.Input.Types.TryGetValue(typeRef.Name, out var type))
      return $"{typeName}()";

    var args = new List<string>();

    if (type.Extends != null)
    {
      var parentPropName = GetParentPropName(type.Extends);
      args.Add($"{parentPropName}: {BuildParentConstructorCall(type.Extends, ctx)}");
    }

    foreach (var propName in type.Properties.Keys)
    {
      args.Add($"{propName}: {propName}");
    }

    return $"{typeName}({string.Join(", ", args)})";
  }

  private static void WriteNonFlagsEnum(TypeSpecification type, string typename, IndentedStringBuilder output)
  {
    output.WriteLine($"public enum {typename}: RawRepresentable, CodingKeyRepresentable, Identifiable, CaseIterable, Codable, Equatable, Hashable, Sendable {{");
    using (output.Indentation)
    {
      var values = type.EnumValues.OrderBy(v => v.Value.Value);

      foreach (var (name, _) in values)
      {
        var safeName = SafePropertyNames.Contains(name) ? $"`{name}`" : name;
        output.WriteLine($"case {safeName}");
      }
      output.WriteLine("/// This case is used to denote an enum case which is not documented. This might occur when the Swift SDK hasn't been updated.");
      output.WriteLine("/// The `Int` argument denotes the rawValue. The `String?` argument denotes the possible coding key value of the undocumented case.");
      output.WriteLine("case undocumented(Int, String? = nil)");

      output.WriteLine();
      output.WriteLine("public var id: Self { self }");
      output.WriteLine();

      output.WriteLine($"public static var allCases: [{typename}]");
      using (output.BracedIndentation)
      {
        output.WriteLine("[");
        foreach (var (name, _) in values)
        {
          var safeName = SafePropertyNames.Contains(name) ? $"`{name}`" : name;
          output.WriteLine($".{safeName},");
        }
        output.WriteLine("]");
      }
      output.WriteLine();

      output.WriteLine("public init(rawValue: Int)");
      using (output.BracedIndentation)
      {
        output.WriteLine("switch rawValue");
        using (output.BracedIndentation)
        {
          foreach (var (name, value) in values)
          {
            output.WriteLine($"case {value.Value}: self = .{name}");
          }
          output.WriteLine("default: self = .undocumented(rawValue)");
        }
      }

      output.WriteLine();

      output.WriteLine("public var rawValue: Int");
      using (output.BracedIndentation)
      {
        output.WriteLine("switch self");
        using (output.BracedIndentation)
        {
          foreach (var (name, value) in values)
          {
            output.WriteLine($"case .{name}: return {value.Value}");
          }
          output.WriteLine("case .undocumented(let int, _): return int");
        }
      }

      // If this enum is used as a dictionary key, the stringValue should be used as the codingkey.
      // We use `CodingKeyRepresentable (https://developer.apple.com/documentation/swift/codingkeyrepresentable)` for this.
      output.WriteLine("public var stringValue: String");
      using (output.BracedIndentation)
      {
        output.WriteLine("switch self");
        using (output.BracedIndentation)
        {
          foreach (var (name, _) in values)
          {
            output.WriteLine($"case .{name}: return \"{name}\"");
          }
          output.WriteLine("case let .undocumented(int, string): return string ?? \"Undocumented (value: \\(int))\"");
        }
      }

      output.WriteLine();
      output.WriteLine("public var codingKey: CodingKey");
      using (output.BracedIndentation)
      {
        output.WriteLine("EnumCodingKey(stringValue: stringValue)");
      }

      output.WriteLine();
      output.WriteLine("/// If the coding key is an undocumented `String`, the `.undocumented` case will be assigned to self with`rawValue = -1`.");
      output.WriteLine("public init?<T>(codingKey: T) where T: CodingKey");
      using (output.BracedIndentation)
      {
        output.WriteLine("switch codingKey.stringValue");
        using (output.BracedIndentation)
        {
          foreach (var (name, _) in values)
          {
            output.WriteLine($"case \"{name}\": self = .{name}");
          }

          output.WriteLine("default: self = .undocumented(codingKey.intValue ?? -1, codingKey.stringValue)");
        }
      }

      output.WriteLine();
      output.WriteLine("public init?(rawValue: String)");
      using (output.BracedIndentation)
      {
        output.WriteLine("switch rawValue");
        using (output.BracedIndentation)
        {
          foreach (var (name, _) in type.EnumValues.OrderBy(v => v.Value.Value))
          {
            var safeName = SafePropertyNames.Contains(name) ? $"`{name}`" : name;
            output.WriteLine($"case \"{name}\": self = .{safeName}");
          }
          output.WriteLine("default: return nil");
        }
      }

      WriteIntRawValueInit(output);
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

      foreach (var (value, name, _) in type.EnumValues.ToTotals())
      {
        if (value == 0) continue;
        output.WriteLine($"public static let {name} = {typename}(rawValue: {value})");
      }

      output.WriteLine();
      output.WriteLine("public init?(rawValue: String)");
      using (output.BracedIndentation)
      {
        output.WriteLine("switch rawValue");
        using (output.BracedIndentation)
        {
          foreach (var (value, name, _) in type.EnumValues.ToTotals())
          {
            if (value == 0) continue;
            output.WriteLine($"case \"{name}\": self = .{name}");
          }
          output.WriteLine("default: return nil");
        }
      }

      WriteIntRawValueInit(output);
    }

    output.WriteLine("}");
  }

  private static void WriteIntRawValueInit(IndentedStringBuilder output)
  {
      output.WriteLine();
      output.WriteLine("public init(from decoder: any Decoder) throws");
      using (output.BracedIndentation)
      {
        output.WriteLine("let container = try decoder.singleValueContainer()");
        output.WriteLine("let rawValue = try container.decode(IntWrapper.self).unwrapped");
        output.WriteLine("self.init(rawValue: rawValue)");
      }
  }

  /// <summary>
  /// Returns the namespace prefix needed when referencing a property's type from outside the type
  /// that defines the property. AllowedValues enums and Option payloads are generated as inline
  /// nested types and are not visible outside the owning type without an explicit prefix.
  /// </summary>
  private static string GetFlatInitTypePrefix(PropertySpecification prop, string typeCtx, string currentTypeId, ApiDefinitionModel input)
  {
    if (typeCtx == currentTypeId) return "";
    if (prop.AllowedValues.Any() || prop.Type.Name == ApiSpecConsts.Specials.Option)
      return $"{GetTypeName(typeCtx, input)}.";
    return "";
  }

  private static string GetPropTypeName(PropertySpecification ps, string name, string? typeContext, OutputContext<SwiftOptions> ctx, bool forceNotNullable = false, bool forceNullable = false, string typePrefix = "")
  {
    var n = OptionalSuffix(ps.Type, forceNotNullable, forceNullable, ps.Skippable);
    var typeName = GetCorePropTypeName(ps, name, typeContext, ctx, forceNotNullable, forceNullable, typePrefix);
    return (ps.Skippable && ps.Type.Nullable ? $"Maybe<{typeName}>" : typeName) + n;
  }

  private static string GetCorePropTypeName(PropertySpecification ps, string name, string? typeContext, OutputContext<SwiftOptions> ctx, bool forceNotNullable = false, bool forceNullable = false, string typePrefix = "")
  {
    var typeReference = ps.Type;
    if (ps.AllowedValues.Any()) return $"{typePrefix}{name}Values";
    if (typeReference is { Name: ApiSpecConsts.Specials.Option }) return $"{typePrefix}{name}Payload";
    return GetTypeName(typeReference, ctx, true);
  }

  private static string GetPropDefault(TypeReference typeReference, bool skippable = false)
  {
    return typeReference.Nullable || skippable ? "nil" : string.Empty;
  }

  private static string OptionalSuffix(TypeReference typeReference, bool forceNotNullable = false, bool forceNullable = false, bool skippable = false)
  {
    return (typeReference.Nullable || skippable) && !forceNotNullable || forceNullable ? "?" : string.Empty;
  }

  private static string GetTypeName(TypeReference typeReference, OutputContext<SwiftOptions> ctx, bool forceNotNullable = false, bool forceNullable = false)
  {
    var n = OptionalSuffix(typeReference, forceNotNullable, forceNullable);

    if (typeReference is { Name: ApiSpecConsts.String or ApiSpecConsts.Duration }) return $"String{n}";
    if (typeReference is { Name: ApiSpecConsts.Binary }) return $"Data{n}";
    if (typeReference is { Name: ApiSpecConsts.ID }) return $"Int{n}";
    if (typeReference is { Name: ApiSpecConsts.Int16 or ApiSpecConsts.Int32 or ApiSpecConsts.Int64 }) return $"Int{n}";
    if (typeReference is { Name: ApiSpecConsts.Float128 }) return $"Decimal{n}";
    if (typeReference is { Name: ApiSpecConsts.Float64 }) return $"Double{n}";
    if (typeReference is { Name: ApiSpecConsts.Float32 }) return $"Float{n}";
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
    if (typeReference is { Name: ApiSpecConsts.WellKnown.IProductSearchItem }) return $"ProductDetails{n}";
    if (typeReference is { Name: ApiSpecConsts.Object }) return $"[String: {ctx.Options.AnyCodableName}]{n}";
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
    if (id == "EVA.Core.Users.Subscriptions.UserDto") return "EVACoreSubscriptionsUserDto";
    if (id == "EVA.Core.Users.Subscriptions.OrganizationUnitDto") return "EVACoreSubscriptionsOrganizationUnitDto";
    if (id == "EVA.Core.Orders.Dto.CompanyDto") return "EVACoreOrdersCompanyDto";
    var reference = input.Types[id];
    var assembly = reference.Assembly;
    assembly = assembly.Replace(".Services", string.Empty);

    var typeName = reference.TypeName.Replace("`", string.Empty).Replace("+", string.Empty);

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

    var typeName = type.TypeName.Replace("`", string.Empty).Replace("+", string.Empty);

    return $"{assembly}{typeName}".Replace(".", string.Empty);
  }

  private static void WriteComment(string s, IndentedStringBuilder output)
  {
    foreach (var line in s.Split('\n').Select(x => x.Trim('\r')))
    {
      output.WriteLine($"/// {line}");
    }
  }

  private static string EscapeString(string comment)
  {
    return comment.Replace("\"", "\\\"");
  }
}