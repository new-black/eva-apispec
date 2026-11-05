using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.zod;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Outputs.zod;

internal class ZodOutput : IApiOutput<ZodOptions>
{
  public string? OutputPattern => null;

  public bool GetForcedTransformations(ZodOptions _, IApiTransform x) => x is RemoveInheritance;

  private const string IProductSearchItem = "EVA.Core.Search.IProductSearchItem";

  private sealed class State(EvaApi api)
  {
    internal EvaApi Api { get; } = api;

    /// <summary>
    /// The request and response types of the services.
    /// </summary>
    internal HashSet<string> ServiceTypes { get; } = api.Services.SelectMany(s => new[] { s.RequestTypeID, s.ResponseTypeID }).ToHashSet();
  }

  public async Task Write(ApiOutputContext<ZodOptions> ctx)
  {
    var (api, _, writer, _) = ctx;
    var state = new State(api);

    foreach (var (typeID, type) in api.Types)
    {
      var o = new IndentedStringBuilder(2);
      o.WriteLine("import { z } from 'zod';");

      var references = new HashSet<string>();
      var typeO = new IndentedStringBuilder(2);
      WriteType(typeID, type, typeO, state, references);

      foreach (var reference in references)
      {
        if(reference == typeID) continue;
        o.WriteLine($"import {{ {TypeNameToTypescriptTypeName(state, reference, null)} }} from './{reference}';");
      }
      o.WriteLine();
      o.WriteLine(typeO.ToString());

      await writer.WriteFileAsync($"{typeID}.ts", o.ToString());
    }
  }

  private static void WriteType(string id, EvaType type, IndentedStringBuilder o, State state, HashSet<string> references)
  {
    var api = state.Api;

    if (type.IsEnum)
    {
      if (type.IsFlags)
      {
        o.WriteLine($"export const {TypeNameToTypescriptTypeName(state, id, null)} = z.number();");
      }
      else
      {
        var enumTypeName = TypeNameToTypescriptTypeName(state, id, null);
        var constName = enumTypeName.Replace("Schema", "");
        
        var enumValues = type.EnumValuesByValue;
        
        // Add JSDoc comment for the const object
        o.WriteLine("/**");
        if (type.Description != null)
        {
          WriteCommentLines(o, type.Description);
          if (enumValues.Any())
          {
            o.WriteLine(" *");
          }
        }
        if (enumValues.Any())
        {
          o.WriteLine(" * @enum");
          foreach (var (name, value, description) in enumValues)
          {
            if (description != null)
            {
              o.WriteLine($" * @property {name} ({value}) - {description}");
            }
            else
            {
              o.WriteLine($" * @property {name} ({value})");
            }
          }
        }
        o.WriteLine(" */");
        
        // Generate the const object
        o.WriteLine($"export const {constName} = {{");
        using (o.Indentation)
        {
          foreach (var (name, value, description) in enumValues)
          {
            if (description != null)
            {
              o.WriteLine($"  /** `{name}` - {description} */");
            }
            o.WriteLine($"  {name}: {value},");
          }
        }
        o.WriteLine("} as const;");
        o.WriteLine();
        
        // Generate the schema using z.nativeEnum
        o.WriteLine($"export const {enumTypeName} = z.nativeEnum({constName});");
      }
    }
    else if (id == IProductSearchItem)
    {
      var fixedTypeName = TypeNameToTypescriptTypeName(state, id, null);
      o.WriteLine($"export const {fixedTypeName} = z.record(z.string(), z.any().nullable())");
    }
    else
    {
      var fixedTypeName = TypeNameToTypescriptTypeName(state, id, null);

      if (type.TypeParameters.Any())
      {
        var typeArgument1 = string.Join(", ", type.TypeParameters.Select(x => $"{x} extends z.ZodType"));
        var typeArgument2 = string.Join(", ", type.TypeParameters.Select(x => $"{x}Schema: {x}"));
        o.WriteLine($"function __{fixedTypeName}<{typeArgument1}>({typeArgument2}) {{ return z.object({{");
      }
      else
      {
        o.WriteLine($"const __{fixedTypeName} = z.object({{");
      }

      var recursiveProperties = type.Properties.Where(p =>
        {
          foreach (var x in p.Value.Type.Descendants())
          {
            if (x is NamedRef named && api.TryGetType(named.TypeID) is { } typeSpec)
            {
              foreach (var x2 in typeSpec.Properties)
              {
                foreach (var x3 in x2.Value.Type.Descendants())
                {
                  if (x3 is NamedRef { TypeID: var x3ID } && x3ID == id)
                  {
                    return true;
                  }
                }
              }
            }
          }

          return false;
        })
        .Select(x => x.Key)
        .ToList();

      using (o.Indentation)
      {
        foreach (var (propName, propSpec) in type.Properties)
        {
          if (recursiveProperties.Contains(propName)) continue;
          WriteProperty(type, o, state, references, propName, propSpec, false);
        }
      }

      if (type.TypeParameters.Any())
      {
        o.WriteLine("});}");
      }
      else
      {
        o.WriteLine("});");
      }

      if (recursiveProperties.Any())
      {
        o.WriteLine($"type _{fixedTypeName} = z.infer<typeof __{fixedTypeName}> & {{");
        using (o.Indentation)
        {
          foreach (var (propName, propSpec) in type.Properties)
          {
            if (!recursiveProperties.Contains(propName)) continue;
            WritePropertyAsType(type, o, state, references, propName, id, propSpec);
          }
        }
        o.WriteLine("};");

        o.WriteLine($"export const {fixedTypeName}: z.ZodType<_{fixedTypeName}> = __{fixedTypeName}.extend({{");
        using (o.Indentation)
        {
          foreach (var (propName, propSpec) in type.Properties)
          {
            if (!recursiveProperties.Contains(propName)) continue;
            WriteProperty(type, o, state, references, propName, propSpec, true);
          }
        }

        o.WriteLine("});");
      }
      else
      {
        o.WriteLine($"export const {fixedTypeName} = __{fixedTypeName};");
      }

      o.WriteLine();
    }
  }

  private static void WriteProperty(
    EvaType type,
    IndentedStringBuilder o,
    State state,
    HashSet<string> references,
    string propName,
    EvaProperty propSpec,
    bool wrapLazy)
  {
    var lp = wrapLazy ? "z.lazy(() => " : string.Empty;
    var ls = wrapLazy ? ")" : string.Empty;
    var propertyType = propSpec.Type;

    if (propertyType.Nullable && !propSpec.Skippable)
    {
      o.WriteLine($"{propName}: {lp}{ToReference(state, propSpec, references, false)}.optional(){ls},");
    }
    else if (propertyType.Nullable && propSpec.Skippable)
    {
      o.WriteLine($"{propName}: {lp}{ToReference(state, propSpec, references)}.optional(){ls},");
    }
    else if (!propertyType.Nullable && !propSpec.Skippable)
    {
      // "primitive values" are always optional because they have a default value that is not `null`
      // we only want to do this in request-only types
      if (type.Usage.request
          && propertyType is PrimitiveRef { Kind: PrimitiveKind.Bool or PrimitiveKind.Int32 or PrimitiveKind.Int64 })
      {
        o.WriteLine($"{propName}: {lp}{ToReference(state, propSpec, references)}.optional(){ls},");
      }
      else if (type.Usage.request
               && propertyType is NamedRef named
               && state.Api.TryGetType(named.TypeID) is { EnumValues.Count: > 0 })
      {
        // While an enum is not a primitive, it is still optional
        o.WriteLine($"{propName}: {lp}{ToReference(state, propSpec, references)}.optional(){ls},");
      }
      else
      {
        o.WriteLine($"{propName}: {lp}{ToReference(state, propSpec, references)}{ls},");
      }
    }
    else if (!propertyType.Nullable && propSpec.Skippable)
    {
      o.WriteLine($"{propName}: {lp}{ToReference(state, propSpec, references)}.optional(){ls},");
    }
  }

  private static void WritePropertyAsType(
    EvaType type,
    IndentedStringBuilder o,
    State state,
    HashSet<string> references,
    string propName,
    string self,
    EvaProperty propSpec)
  {
    var propertyType = propSpec.Type;

    if (propertyType.Nullable && !propSpec.Skippable)
    {
      o.WriteLine($"{propName}?: {ToReferenceAsType(state, propSpec, references, self, false)},");
    }
    else if (propertyType.Nullable && propSpec.Skippable)
    {
      o.WriteLine($"{propName}?: {ToReferenceAsType(state, propSpec, references, self)},");
    }
    else if (!propertyType.Nullable && !propSpec.Skippable)
    {
      // "primitive values" are always optional because they have a default value that is not `null`
      // we only want to do this in request-only types
      if (type.Usage.request
          && propertyType is PrimitiveRef { Kind: PrimitiveKind.Bool or PrimitiveKind.Int32 or PrimitiveKind.Int64 })
      {
        o.WriteLine($"{propName}?: {ToReferenceAsType(state, propSpec, references, self)},");
      }
      else if (type.Usage.request
               && propertyType is NamedRef named
               && state.Api.TryGetType(named.TypeID) is { EnumValues.Count: > 0 })
      {
        // While an enum is not a primitive, it is still optional
        o.WriteLine($"{propName}?: {ToReferenceAsType(state, propSpec, references, self)},");
      }
      else
      {
        o.WriteLine($"{propName}: {ToReferenceAsType(state, propSpec, references, self)},");
      }
    }
    else if (!propertyType.Nullable && propSpec.Skippable)
    {
      o.WriteLine($"{propName}?: {ToReferenceAsType(state, propSpec, references, self)},");
    }
  }

  private static string ToReference(State state, EvaProperty ps, HashSet<string> references, bool? overrideNullable = null)
  {
    var type = ps.Type;

    if (type is PrimitiveRef { Kind: PrimitiveKind.String or PrimitiveKind.ID } && ps.AllowedValues.Any())
    {
      var values = ps.AllowedValues.Select(EscapeForString).Concat(overrideNullable ?? type.Nullable ? ["null"] : []);
      return $"z.enum([{string.Join(", ", values)}])";
    }

    // Option
    if (type is OptionRef option)
    {
      // Only use types from this assembly, and add an extender. This extender is patched later on.
      var options = option.Options;
      var nullable = overrideNullable ?? type.Nullable || options.Any(o => o.Nullable);

      var extra = new List<string?>();
      if (nullable) extra.Add("z.null()");

      var allReferences = (options.Any() ? options.Cast<TypeRef>() : [option.Shared])
        .Select(tr => ToReference(state, tr, references, overrideNullable)).Concat(extra).ToList();

      if (allReferences.Count == 1) return allReferences[0]!;
      return $"z.union([{string.Join(", ", allReferences)}])";
    }

    return ToReference(state, type, references, overrideNullable);
  }

  private static string ToReference(State state, TypeRef typeReference, HashSet<string> references, bool? overrideNullable = null)
  {
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? ".nullable()" : string.Empty;

    var preset = typeReference switch
    {
      PrimitiveRef { Kind: PrimitiveKind.ID or PrimitiveKind.String or PrimitiveKind.Date or PrimitiveKind.Binary or PrimitiveKind.Guid or PrimitiveKind.Duration } => $"z.string(){n}",
      PrimitiveRef { Kind: PrimitiveKind.Bool } => $"z.boolean(){n}",
      PrimitiveRef { Kind: PrimitiveKind.Int32 or PrimitiveKind.Int64 or PrimitiveKind.Int16 or PrimitiveKind.Float32 or PrimitiveKind.Float64 or PrimitiveKind.Decimal } => $"z.number(){n}",
      ArrayRef array => $"z.array({ToReference(state, array.Items, references, false)}){n}",
      TypeParameterRef parameter => parameter.Name + "Schema",
      // Key will always be a string
      MapRef map => $"z.record(z.string(), {ToReference(state, map.Value, references)}){n}",
      _ => null
    };

    if (preset != null) return preset;

    // Object
    if (typeReference is PrimitiveRef { Kind: PrimitiveKind.Object })
    {
      return $"z.record(z.string(), z.any()){n}";
    }

    // Any
    if (typeReference is PrimitiveRef { Kind: PrimitiveKind.Any })
    {
      return $"z.any(){n}";
    }

    var named = (NamedRef)typeReference;

    // Apparently a type
    if (!named.Arguments.Any())
    {
      if (state.Api.TryGetType(named.TypeID)?.IsEnum ?? true)
      {
        return TypeNameToTypescriptTypeName(state, named.TypeID, references);
      }
      else
      {
        return TypeNameToTypescriptTypeName(state, named.TypeID, references);
      }
    }

    var args = new List<string>();
    foreach (var a in named.Arguments)
    {
      args.Add(ToReference(state, a, references));
    }

    return $"{TypeNameToTypescriptTypeName(state, named.TypeID, references)}({string.Join(", ", args)})";
  }

  private static string ToReferenceAsType(State state, EvaProperty ps, HashSet<string> references, string self, bool? overrideNullable = null)
  {
    var type = ps.Type;

    if (type is PrimitiveRef { Kind: PrimitiveKind.String or PrimitiveKind.ID } && ps.AllowedValues.Any())
    {
      return string.Join(" | ", ps.AllowedValues.Select(EscapeForString).Concat(overrideNullable ?? type.Nullable ? ["null"] : []));
    }

    // Option
    if (type is OptionRef option)
    {
      var options = option.Options;
      var nullable = overrideNullable ?? type.Nullable || options.Any(o => o.Nullable);

      var extra = new List<string?>();
      if (nullable) extra.Add("null");

      var allReferences = (options.Any() ? options.Cast<TypeRef>() : [option.Shared])
        .Select(tr => ToReferenceAsType(state, tr, references, self, overrideNullable)).Concat(extra);
      return string.Join(" | ", allReferences);
    }

    return ToReferenceAsType(state, type, references, self, overrideNullable);
  }

  private static string ToReferenceAsType(State state, TypeRef typeReference, HashSet<string> references, string self, bool? overrideNullable = null)
  {
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? " | null" : string.Empty;

    var preset = typeReference switch
    {
      PrimitiveRef { Kind: PrimitiveKind.ID or PrimitiveKind.String or PrimitiveKind.Date or PrimitiveKind.Binary or PrimitiveKind.Guid or PrimitiveKind.Duration } => $"string{n}",
      PrimitiveRef { Kind: PrimitiveKind.Bool } => $"boolean{n}",
      PrimitiveRef { Kind: PrimitiveKind.Int32 or PrimitiveKind.Int64 or PrimitiveKind.Int16 or PrimitiveKind.Float32 or PrimitiveKind.Float64 or PrimitiveKind.Decimal } => $"number{n}",
      ArrayRef array => $"{ToReferenceAsType(state, array.Items, references, self, false)}[]{n}",
      TypeParameterRef parameter => parameter.Name,
      // Key will always be a string
      MapRef map => $"Record<string,{ToReferenceAsType(state, map.Value, references, self)}>{n}",
      _ => null
    };

    if (preset != null) return preset;

    // Object
    if (typeReference is PrimitiveRef { Kind: PrimitiveKind.Object })
    {
      return $"Record<string, any>{n}";
    }

    // Any
    if (typeReference is PrimitiveRef { Kind: PrimitiveKind.Any })
    {
      return $"any{n}";
    }

    var named = (NamedRef)typeReference;

    // Apparently a type
    var t = TypeNameToTypescriptTypeName(state, named.TypeID, references);
    if (self == named.TypeID)
    {
      t = "_" + t;
      return t;
    }

    if (!named.Arguments.Any())
    {
      return $"z.infer<typeof {t}>";
    }

    var args = new List<string>();
    foreach (var a in named.Arguments)
    {
      args.Add(ToReferenceAsType(state, a, references, self));
    }

    return $"{t}<{string.Join(", ", args)}>";
  }

  private static string EscapeForString(string s)
  {
    return $"'{s.Replace("'", @"\'")}'";
  }

  private static void WriteCommentLines(IndentedStringBuilder o, string comment)
  {
    var c = comment.Trim();
    if (string.IsNullOrWhiteSpace(c)) return;
    foreach (var line in c.Split('\n'))
    {
      var trimmed = line.Trim();
      if (!string.IsNullOrWhiteSpace(trimmed))
      {
        o.WriteLine($" * {trimmed}");
      }
    }
  }

  private static void WriteComment(IndentedStringBuilder o, string comment)
  {
    var c = comment.Trim();
    if (string.IsNullOrWhiteSpace(c)) return;
    o.WriteLine("/**");
    WriteCommentLines(o, comment);
    o.WriteLine(" */");
  }

  private static string TypeNameToTypescriptTypeName(State state, string name, HashSet<string>? references)
  {
    references?.Add(name);

    // For service types
    {
      if (state.ServiceTypes.Contains(name))
      {
        var idx = name.IndexOf('`');
        name = idx == -1 ? name : name[..idx];

        idx = name.LastIndexOf('.');
        name = idx == -1 ? name : name[(idx + 1)..];

        return name + "Schema";
      }
    }

    // Other types
    {
      var spec = state.Api.GetType(name);
      if (name.StartsWith(spec.Assembly + "."))
      {
        name = name[(spec.Assembly.Length + 1)..];
      }

      var idx = name.IndexOf('`');
      name = idx == -1 ? name : name[..idx];

      var result = name.Replace(".", string.Empty).Replace("+", "_");
      return result + "Schema";
    }
  }
}
