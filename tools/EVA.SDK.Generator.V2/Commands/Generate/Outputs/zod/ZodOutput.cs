using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.zod;

internal class ZodOutput : IOutput<ZodOptions>
{
  public string? OutputPattern => null;

  public bool GetForcedTransformations(ZodOptions _, INamedTransform x) => x is RemoveInheritance;

  public async Task Write(OutputContext<ZodOptions> ctx)
  {
    var (input, options, writer, _) = ctx;

    foreach (var (typeID, type) in input.Types)
    {
      var o = new IndentedStringBuilder(2);
      o.WriteLine("import { z } from 'zod';");

      var references = new HashSet<string>();
      var typeO = new IndentedStringBuilder(2);
      WriteType(typeID, type, typeO, input, references);

      foreach (var reference in references)
      {
        if(reference == typeID) continue;
        o.WriteLine($"import {{ {TypeNameToTypescriptTypeName(input, reference, null)} }} from './{reference}';");
      }
      o.WriteLine();
      o.WriteLine(typeO.ToString());

      await writer.WriteFileAsync($"{typeID}.ts", o.ToString());
    }
  }

  private static void WriteType(string id, TypeSpecification type, IndentedStringBuilder o, ApiDefinitionModel input, HashSet<string> references)
  {
    if (type.EnumIsFlag.HasValue)
    {
      if (type.EnumIsFlag.Value)
      {
        o.WriteLine($"export const {TypeNameToTypescriptTypeName(input, id, null)} = z.number();");
      }
      else
      {
        var values = type.EnumValues.ToTotals().Select(x => x.Value).Order().Select(x => $"z.literal({x})");

        if (values.Count() == 1)
        {
          o.WriteLine($"export const {TypeNameToTypescriptTypeName(input, id, null)} = {values.First()};");
        }
        else
        {
          o.WriteLine($"export const {TypeNameToTypescriptTypeName(input, id, null)} = z.union([{string.Join(", ", values)}]);");
        }
      }
    }
    else if (id == ApiSpecConsts.WellKnown.IProductSearchItem)
    {
      var fixedTypeName = TypeNameToTypescriptTypeName(input, id, null);
      o.WriteLine($"export const {fixedTypeName} = z.record(z.string(), z.any().nullable())");
    }
    else
    {
      var fixedTypeName = TypeNameToTypescriptTypeName(input, id, null);

      if (type.TypeArguments.Any())
      {
        var typeArgument1 = string.Join(", ", type.TypeArguments.Select(x => $"{x[1..]} extends z.ZodTypeAny"));
        var typeArgument2 = string.Join(", ", type.TypeArguments.Select(x => $"{x[1..]}Schema: {x[1..]}"));
        o.WriteLine($"function __{fixedTypeName}<{typeArgument1}>({typeArgument2}) {{ return z.object({{");
      }
      else
      {
        o.WriteLine($"const __{fixedTypeName} = z.object({{");
      }

      var recursiveProperties = type.Properties.Where(p =>
        {
          foreach (var x in p.Value.Type.EnumerateAllTypeReferences())
          {
            if (input.Types.TryGetValue(x.Name, out var typeSpec))
            {
              foreach (var x2 in typeSpec.Properties)
              {
                foreach (var x3 in x2.Value.Type.EnumerateAllTypeReferences())
                {
                  if (x3.Name == id)
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
          WriteProperty(type, o, input, references, propName, propSpec, false);
        }
      }

      if (type.TypeArguments.Any())
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
            WritePropertyAsType(type, o, input, references, propName, id, propSpec);
          }
        }
        o.WriteLine("};");

        o.WriteLine($"export const {fixedTypeName}: z.ZodType<_{fixedTypeName}> = __{fixedTypeName}.extend({{");
        using (o.Indentation)
        {
          foreach (var (propName, propSpec) in type.Properties)
          {
            if (!recursiveProperties.Contains(propName)) continue;
            WriteProperty(type, o, input, references, propName, propSpec, true);
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
    TypeSpecification type,
    IndentedStringBuilder o,
    ApiDefinitionModel input,
    HashSet<string> references,
    string propName,
    PropertySpecification propSpec,
    bool wrapLazy)
  {
    var lp = wrapLazy ? "z.lazy(() => " : string.Empty;
    var ls = wrapLazy ? ")" : string.Empty;

    if (propSpec.Type.Nullable && !propSpec.Skippable)
    {
      o.WriteLine($"{propName}: {lp}{ToReference(input, propSpec, references, false)}.optional(){ls},");
    }
    else if (propSpec.Type.Nullable && propSpec.Skippable)
    {
      o.WriteLine($"{propName}: {lp}{ToReference(input, propSpec, references)}.optional(){ls},");
    }
    else if (!propSpec.Type.Nullable && !propSpec.Skippable)
    {
      // "primitive values" are always optional because they have a default value that is not `null`
      // we only want to do this in request-only types
      if (type.Usage is { Request: true }
          && propSpec.Type.Name is ApiSpecConsts.Bool or ApiSpecConsts.Int32 or ApiSpecConsts.Int64)
      {
        o.WriteLine($"{propName}: {lp}{ToReference(input, propSpec, references)}.optional(){ls},");
      }
      else if (type.Usage is { Request: true }
               && input.Types.TryGetValue(propSpec.Type.Name, out var typeSpec)
               && typeSpec.EnumValues.Count > 0)
      {
        // While an enum is not a primitive, it is still optional
        o.WriteLine($"{propName}: {lp}{ToReference(input, propSpec, references)}.optional(){ls},");
      }
      else
      {
        o.WriteLine($"{propName}: {lp}{ToReference(input, propSpec, references)}{ls},");
      }
    }
    else if (!propSpec.Type.Nullable && propSpec.Skippable)
    {
      o.WriteLine($"{propName}: {lp}{ToReference(input, propSpec, references)}.optional(){ls},");
    }
  }

  private static void WritePropertyAsType(
    TypeSpecification type,
    IndentedStringBuilder o,
    ApiDefinitionModel input,
    HashSet<string> references,
    string propName,
    string self,
    PropertySpecification propSpec)
  {
    if (propSpec.Type.Nullable && !propSpec.Skippable)
    {
      o.WriteLine($"{propName}?: {ToReferenceAsType(input, propSpec, references, self, false)},");
    }
    else if (propSpec.Type.Nullable && propSpec.Skippable)
    {
      o.WriteLine($"{propName}?: {ToReferenceAsType(input, propSpec, references, self)},");
    }
    else if (!propSpec.Type.Nullable && !propSpec.Skippable)
    {
      // "primitive values" are always optional because they have a default value that is not `null`
      // we only want to do this in request-only types
      if (type.Usage is { Request: true }
          && propSpec.Type.Name is ApiSpecConsts.Bool or ApiSpecConsts.Int32 or ApiSpecConsts.Int64)
      {
        o.WriteLine($"{propName}?: {ToReferenceAsType(input, propSpec, references, self)},");
      }
      else if (type.Usage is { Request: true }
               && input.Types.TryGetValue(propSpec.Type.Name, out var typeSpec)
               && typeSpec.EnumValues.Count > 0)
      {
        // While an enum is not a primitive, it is still optional
        o.WriteLine($"{propName}?: {ToReferenceAsType(input, propSpec, references, self)},");
      }
      else
      {
        o.WriteLine($"{propName}: {ToReferenceAsType(input, propSpec, references, self)},");
      }
    }
    else if (!propSpec.Type.Nullable && propSpec.Skippable)
    {
      o.WriteLine($"{propName}?: {ToReferenceAsType(input, propSpec, references, self)},");
    }
  }

  private static string ToReference(ApiDefinitionModel input, PropertySpecification ps, HashSet<string> references, bool? overrideNullable = null)
  {
    if (ps.Type.Name == ApiSpecConsts.String && ps.AllowedValues.Any())
    {
      var values = ps.AllowedValues.Select(EscapeForString).Concat(overrideNullable ?? ps.Type.Nullable ? ["null"] : []);
      return $"z.enum([{string.Join(", ", values)}])";
    }

    // Option
    if (ps.Type is { Name: ApiSpecConsts.Specials.Option, Arguments: var options })
    {
      // Only use types from this assembly, and add an extender. This extender is patched later on.
      var nullable = overrideNullable ?? ps.Type.Nullable || options.Any(o => o.Nullable);

      var extra = new List<string?>();
      if (nullable) extra.Add("z.null()");

      var allReferences = (options.Any() ? options : [ps.Type.Shared!])
        .Select(tr => ToReference(input, tr, references, overrideNullable)).Concat(extra).ToList();

      if (allReferences.Count == 1) return allReferences[0];
      return $"z.union([{string.Join(", ", allReferences)}])";
    }

    return ToReference(input, ps.Type, references, overrideNullable);
  }

  private static string ToReference(ApiDefinitionModel input, TypeReference typeReference, HashSet<string> references, bool? overrideNullable = null)
  {
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? ".nullable()" : string.Empty;

    var preset = typeReference switch
    {
      { Name: ApiSpecConsts.ID } => $"z.number(){n}",
      { Name: ApiSpecConsts.String or ApiSpecConsts.Date or ApiSpecConsts.Binary or ApiSpecConsts.Guid or ApiSpecConsts.Duration } => $"z.string(){n}",
      { Name: ApiSpecConsts.Bool } => $"z.boolean(){n}",
      { Name: ApiSpecConsts.Int32 or ApiSpecConsts.Int64 or ApiSpecConsts.Int16 or ApiSpecConsts.Float32 or ApiSpecConsts.Float64 or ApiSpecConsts.Float128 } => $"z.number(){n}",
      { Name: ApiSpecConsts.Specials.Array, Arguments.Length: 1 } => $"z.array({ToReference(input, typeReference.Arguments[0], references, false)}){n}",
      _ when typeReference.Name.StartsWith('_') => typeReference.Name[1..] + "Schema",
      // Key will always be a string
      { Name: ApiSpecConsts.Specials.Map, Arguments.Length: 2 } => $"z.record(z.string(), {ToReference(input, typeReference.Arguments[1], references)}){n}",
      _ => null
    };

    if (preset != null) return preset;

    // Object
    if (typeReference is { Name: ApiSpecConsts.Object })
    {
      return $"z.record(z.string(), z.any()){n}";
    }

    // Any
    if (typeReference is { Name: ApiSpecConsts.Any })
    {
      return $"z.any(){n}";
    }

    // Apparently a type
    if (!typeReference.Arguments.Any())
    {
      if (input.Types.GetValueOrDefault(typeReference.Name)?.EnumIsFlag.HasValue ?? true)
      {
        return TypeNameToTypescriptTypeName(input, typeReference.Name, references);
      }
      else
      {
        return TypeNameToTypescriptTypeName(input, typeReference.Name, references);
      }
    }

    var args = new List<string>();
    foreach (var a in typeReference.Arguments)
    {
      args.Add(ToReference(input, a, references));
    }

    return $"{TypeNameToTypescriptTypeName(input, typeReference.Name, references)}({string.Join(", ", args)})";
  }

  private static string ToReferenceAsType(ApiDefinitionModel input, PropertySpecification ps, HashSet<string> references, string self, bool? overrideNullable = null)
  {
    if (ps.Type.Name == ApiSpecConsts.String && ps.AllowedValues.Any())
    {
      return string.Join(" | ", ps.AllowedValues.Select(EscapeForString).Concat(overrideNullable ?? ps.Type.Nullable ? ["null"] : []));
    }

    // Option
    if (ps.Type is { Name: ApiSpecConsts.Specials.Option, Arguments: var options })
    {
      var nullable = overrideNullable ?? ps.Type.Nullable || options.Any(o => o.Nullable);

      var extra = new List<string?>();
      if (nullable) extra.Add("null");

      var allReferences = (options.Any() ? options : [ps.Type.Shared!])
        .Select(tr => ToReferenceAsType(input, tr, references, self, overrideNullable)).Concat(extra);
      return string.Join(" | ", allReferences);
    }

    return ToReferenceAsType(input, ps.Type, references, self, overrideNullable);
  }

  private static string ToReferenceAsType(ApiDefinitionModel input, TypeReference typeReference, HashSet<string> references, string self, bool? overrideNullable = null)
  {
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? " | null" : string.Empty;

    var preset = typeReference switch
    {
      { Name: ApiSpecConsts.ID } => $"number{n}",
      { Name: ApiSpecConsts.String or ApiSpecConsts.Date or ApiSpecConsts.Binary or ApiSpecConsts.Guid or ApiSpecConsts.Duration } => $"string{n}",
      { Name: ApiSpecConsts.Bool } => $"boolean{n}",
      { Name: ApiSpecConsts.Int32 or ApiSpecConsts.Int64 or ApiSpecConsts.Int16 or ApiSpecConsts.Float32 or ApiSpecConsts.Float64 or ApiSpecConsts.Float128 } => $"number{n}",
      { Name: ApiSpecConsts.Specials.Array, Arguments.Length: 1 } => $"{ToReferenceAsType(input, typeReference.Arguments[0], references, self, false)}[]{n}",
      _ when typeReference.Name.StartsWith("_") => typeReference.Name[1..],
      // Key will always be a string
      { Name: ApiSpecConsts.Specials.Map, Arguments.Length: 2 } => $"Record<string,{ToReferenceAsType(input, typeReference.Arguments[1], references, self)}>{n}",
      _ => null
    };

    if (preset != null) return preset;

    // Object
    if (typeReference is { Name: ApiSpecConsts.Object })
    {
      return $"Record<string, any>{n}";
    }

    // Any
    if (typeReference is { Name: ApiSpecConsts.Any })
    {
      return $"any{n}";
    }

    // Apparently a type
    var t = TypeNameToTypescriptTypeName(input, typeReference.Name, references);
    if (self == typeReference.Name)
    {
      t = "_" + t;
      return t;
    }

    if (!typeReference.Arguments.Any())
    {
      return $"z.infer<typeof {t}>";
    }

    var args = new List<string>();
    foreach (var a in typeReference.Arguments)
    {
      args.Add(ToReferenceAsType(input, a, references, self));
    }

    return $"{t}<{string.Join(", ", args)}>";
  }

  private static string EscapeForString(string s)
  {
    return $"'{s.Replace("'", @"\'")}'";
  }

  private static string TypeNameToTypescriptTypeName(ApiDefinitionModel input, string name, HashSet<string>? references)
  {
    references?.Add(name);

    // For service types
    {
      if (input.Services.Any(s => s.RequestTypeID == name || s.ResponseTypeID == name))
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
      var spec = input.Types[name];
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