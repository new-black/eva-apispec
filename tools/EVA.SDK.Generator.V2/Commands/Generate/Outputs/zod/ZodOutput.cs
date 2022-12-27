using System.Text;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.zod;

public class ZodOutput : IOutput<ZodOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedRemoves => new[] { "unused-type-params", "empty-types", "errors", "event-exports", "nested-types" };

  public async Task Write(ApiDefinitionModel input, ZodOptions options)
  {
    foreach (var group in input.GroupByAssembly())
    {
      var assemblyCtx = new AssemblyContext(group.Assembly);
      var o = new IndentedStringBuilder(2);

      // Write namespace
      o.WriteLine($"export namespace {TypescriptOutput.FixNamespace(group.Assembly)} {{");
      using (o.Indentation)
      {
        // Preset types
        if (group.Assembly == "EVA.Core")
        {
          o.WriteLine();
          o.WriteLine("const _literalSchema = z.union([z.string(), z.number(), z.boolean(), z.null()]);");
          o.WriteLine("type _literal = z.infer<typeof _literalSchema>;");
          o.WriteLine("export type _anyValue = _literal | { [key: string]: _anyValue } | _anyValue[];");
          o.WriteLine("export const TAnyValue: z.ZodType<_anyValue> = z.lazy(() => z.union([_literalSchema, z.array(TAnyValue), z.record(TAnyValue)]));");
        }

        // Write the types
        WriteTypes(group, o, input, assemblyCtx);
      }

      o.WriteLine("}");

      // Write the import statements
      var importsBuilder = new StringBuilder();
      foreach (var x in assemblyCtx.ReferencedModules)
      {
        importsBuilder.AppendLine($"import {{ {TypescriptOutput.FixNamespace(x)} }} from '{TypescriptOutput.GetModuleReference(x, null)}';");
      }

      importsBuilder.AppendLine("import { z } from 'zod';");

      importsBuilder.AppendLine();
      importsBuilder.AppendLine(o.ToString());

      await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory, $"{group.Assembly}.ts"), importsBuilder.ToString());
    }
  }

  private void WriteTypes(ApiDefinitionModelExtensions.GroupedApiDefinitionModel group, IndentedStringBuilder o, ApiDefinitionModel input, AssemblyContext ctx)
  {
    var writtenTypes = new HashSet<string>();
    var typesInThisModule = group.Types.Keys.ToHashSet();
    var lazyTypes = new HashSet<string>();

    var typesToWrite = group.Types.Select(kv => (id: kv.Key, type: kv.Value)).ToHashSet();

    while (typesToWrite.Any())
    {
      // Simplest case: write the type if it has no unwritten-dependencies dependencies and its extends type is not a lazy type
      var typeWithoutDependencies = typesToWrite.FirstOrDefault(x =>
        (x.type.Extends == null || !lazyTypes.Contains(x.type.Extends.Name)) && x.type.TypeDependencies.All(dep => !typesInThisModule.Contains(dep) || writtenTypes.Contains(dep)));
      if (typeWithoutDependencies != default)
      {
        var (id, type) = typeWithoutDependencies;
        writtenTypes.Add(id);
        typesToWrite.Remove(typeWithoutDependencies);
        var fixedTypeName = TypescriptOutput.FixTypeName(input, id);

        if (type.EnumIsFlag.HasValue)
        {
          WriteEnum(o, type, fixedTypeName, type.EnumIsFlag.Value);
          continue;
        }

        // Write out the type
        if (type.TypeArguments.Any())
        {
          var args = string.Join(", ", type.TypeArguments.Select(t => $"{t} extends z.ZodTypeAny"));
          var param = string.Join(", ", type.TypeArguments.Select(t => $"{t[1..]}: {t}"));
          o.WriteLine($"export function {fixedTypeName}<{args}>({param}) {{");
          o.WriteIndented(o =>
          {
            o.Write("return ");
            if (type.Extends != null) o.Write($"{ToReference(input, type.Extends, ctx, false)}.merge(");
            o.WriteLine("z.object({");
            o.WriteIndented(o => { WriteProperties(input, ctx, type, o, fixedTypeName); });
            o.Write("})");
            if (type.Extends != null) o.Write(")");
            o.WriteLine(";");
          });
          o.WriteLine("}");
        }
        else
        {
          o.Write($"export const {fixedTypeName} = ");
          if (type.Extends != null) o.Write($"{ToReference(input, type.Extends, ctx, false)}.merge(");
          o.WriteLine("z.object({");
          o.WriteIndented(o => { WriteProperties(input, ctx, type, o, fixedTypeName); });
          o.Write("})");
          if (type.Extends != null) o.Write(")");
          o.WriteLine(";");
        }

        continue;
      }

      // At this point we have circular dependencies. We will just randomly choose one (although we prefer types that only reference itself).
      // This is not optimal, but it is probably good enough.
      {
        var typeToWrite = typesToWrite.FirstOrDefault(x => x.type.TypeDependencies.All(dep => !typesInThisModule.Contains(dep) || writtenTypes.Contains(dep) || dep == x.id));
        typeToWrite = typeToWrite == default ? typesToWrite.First() : typeToWrite;

        var (id, type) = typeToWrite;
        writtenTypes.Add(id);
        typesToWrite.Remove(typeToWrite);
        lazyTypes.Add(id);
        var fixedTypeName = TypescriptOutput.FixTypeName(input, id);

        if (!type.TypeArguments.Any())
        {
          o.WriteLine($"interface {fixedTypeName}Schema {{");
          using (o.Indentation)
          {
            WriteInterfaceProperties(input, ctx, type, o, fixedTypeName);
          }
          o.WriteLine("}");

          o.WriteLine($"export const {fixedTypeName}: z.ZodType<{fixedTypeName}Schema> = z.lazy(() => z.object({{");
          using (o.Indentation)
          {
            WriteProperties(input, ctx, type, o, fixedTypeName);
          }
          o.WriteLine("}));");
        }
        else
        {
          throw new Exception("Self referencing generics are not supported");
        }
      }
    }
  }

  private static void WriteEnum(IndentedStringBuilder o, TypeSpecification type, string fixedTypeName, bool isFlag)
  {
    if (isFlag)
    {
      o.WriteLine($"export const {fixedTypeName} = z.number();");
    }
    else
    {
      o.WriteLine($"export enum {fixedTypeName}Schema {{");
      o.WriteIndented(o =>
      {
        foreach (var v in type.EnumValues)
        {
          o.WriteLine($"{v.Key} = {v.Value.Value},");
        }
      });
      o.WriteLine("}");
      o.WriteLine($"export const {fixedTypeName} = z.nativeEnum({fixedTypeName}Schema);");
    }
  }

  private void WriteInterfaceProperties(ApiDefinitionModel input, AssemblyContext ctx, TypeSpecification type, IndentedStringBuilder o, string fixedTypeName)
  {
    foreach (var (propName, propSpec) in type.Properties)
    {
      if (propSpec.Type.Nullable && !propSpec.Skippable)
      {
        o.WriteLine($"{propName}?: {ToInterfaceReference(input, propSpec, propName, fixedTypeName, ctx)},");
      }
      else if (propSpec.Type.Nullable && propSpec.Skippable)
      {
        o.WriteLine($"{propName}?: {ToInterfaceReference(input, propSpec, propName, fixedTypeName, ctx)},");
      }
      else if (!propSpec.Type.Nullable && !propSpec.Skippable)
      {
        o.WriteLine($"{propName}: {ToInterfaceReference(input, propSpec, propName, fixedTypeName, ctx)},");
      }
      else if (!propSpec.Type.Nullable && propSpec.Skippable)
      {
        o.WriteLine($"{propName}?: {ToInterfaceReference(input, propSpec, propName, fixedTypeName, ctx)},");
      }
    }
  }

  private string ToInterfaceReference(ApiDefinitionModel input, PropertySpecification ps, string propName, string typeName, AssemblyContext ctx, bool? overrideNullable = null)
  {
    if (ps.Type.Name == "string" && ps.AllowedValues.Any())
    {
      return string.Join(" | ", ps.AllowedValues.Select(TypescriptOutput.EscapeForString).Concat(overrideNullable ?? ps.Type.Nullable ? new[] { "null" } : Array.Empty<string>()));
    }

    // Option
    if (ps.Type is { Name: "option", Arguments: var options })
    {
      // Only use types from this assembly, and add an extender. This extender is patched later on.
      var typesFromCurrentAssembly = options.Where(o => input.Types[o.Name].Assembly == ctx.AssemblyName).ToArray();
      var nullable = overrideNullable ?? ps.Type.Nullable || typesFromCurrentAssembly.Any(o => o.Nullable);

      var allReferences = typesFromCurrentAssembly.Select(tr => ToInterfaceReference(input, tr, ctx, false)).Concat(nullable ? new[] { "null" } : Enumerable.Empty<string>());
      return string.Join(" | ", allReferences);
    }

    return ToInterfaceReference(input, ps.Type, ctx, overrideNullable);
  }

  private string ToInterfaceReference(ApiDefinitionModel input, TypeReference typeReference, AssemblyContext ctx, bool? overrideNullable = null)
  {
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? " | null" : string.Empty;

    var preset = typeReference switch
    {
      { Name: "string" or "date" or "binary" or "guid" or "duration" } => $"string{n}",
      { Name: "bool" } => $"boolean{n}",
      { Name: "int32" or "int64" or "int16" or "float32" or "float64" or "float128" } => $"number{n}",
      //{ Name: "array", Arguments: [var a] } => $"{ToReference(input, a, ctx)}[]{n}",
      { Name: "array", Arguments.Length: 1 } => $"({ToInterfaceReference(input, typeReference.Arguments[0], ctx)})[]{n}",
      //{ Name: ['_', .. var x] } => x,
      _ when typeReference.Name.StartsWith("_") => typeReference.Name[1..],
      //{ Name: "map", Arguments: [var k, var v] } => $"{{[key:{ToReference(input, k, ctx, false)}]:{ToReference(input, v, ctx)}}}{n}",
      { Name: "map", Arguments.Length: 2 } => $"{{[key:{ToInterfaceReference(input, typeReference.Arguments[0], ctx, false)}]:{ToInterfaceReference(input, typeReference.Arguments[1], ctx)}}}{n}",
      _ => null
    };

    if (preset != null) return preset;

    // Object
    if (typeReference is { Name: "object" })
    {
      ctx.RegisterReferencedModule("EVA.Core");
      return ctx.AssemblyName == "EVA.Core" ? $"Record<string, _anyValue>{n}" : $"Record<string, EvaCore._anyValue>{n}";
    }

    // Any
    if (typeReference is { Name: "any" })
    {
      ctx.RegisterReferencedModule("EVA.Core");
      return ctx.AssemblyName == "EVA.Core" ? $"_anyValue{n}" : $"EvaCore._anyValue{n}";
    }

    // Apparently a type
    ctx.RegisterReferencedModule(input.Types[typeReference.Name].Assembly);
    if (!typeReference.Arguments.Any())
    {
      var t = ToReference(input, typeReference, ctx);
      return $"z.infer<typeof {t}>{n}";
    }
    else
    {
      return "unknown";
      // var args = typeReference.Arguments.Select(a => ToReference(input, a, ctx));
      // return $"{GetTypeRef(input, typeReference.Name, ctx)}<{string.Join(", ", args)}>";
    }
  }

  private void WriteProperties(ApiDefinitionModel input, AssemblyContext ctx, TypeSpecification type, IndentedStringBuilder o, string fixedTypeName)
  {
    foreach (var (propName, propSpec) in type.Properties)
    {
      if (propSpec.Type.Nullable && !propSpec.Skippable)
      {
        o.WriteLine($"{propName}: {ToReference(input, propSpec, propName, fixedTypeName, ctx)}.optional(),");
      }
      else if (propSpec.Type.Nullable && propSpec.Skippable)
      {
        o.WriteLine($"{propName}: {ToReference(input, propSpec, propName, fixedTypeName, ctx)}.optional(),");
      }
      else if (!propSpec.Type.Nullable && !propSpec.Skippable)
      {
        o.WriteLine($"{propName}: {ToReference(input, propSpec, propName, fixedTypeName, ctx)},");
      }
      else if (!propSpec.Type.Nullable && propSpec.Skippable)
      {
        o.WriteLine($"{propName}: {ToReference(input, propSpec, propName, fixedTypeName, ctx)}.optional(),");
      }
    }
  }

  private string ToReference(ApiDefinitionModel input, PropertySpecification ps, string propName, string typeName, AssemblyContext ctx, bool? overrideNullable = null)
  {
    if (ps.Type.Name == "string" && ps.AllowedValues.Any())
    {
      return $"z.enum([{string.Join(", ", ps.AllowedValues.Select(TypescriptOutput.EscapeForString))}]){(overrideNullable ?? ps.Type.Nullable ? ".nullable()" : "")}";
    }

    if (ps.Type is { Name: "option", Arguments: var options })
    {
      // TODO: Extenders

      // Easy case, might change due to extenders
      if (options.Length == 1) return ToReference(input, options.First(), ctx, overrideNullable);

      // Only add types from this assembly
      // var typesFromCurrentAssembly = options.Where(o => !input.Types.TryGetValue(o.Name, out var x) || x.Assembly == ctx.AssemblyName);
      var typesFromCurrentAssembly = options;
      var nullable = overrideNullable ?? ps.Type.Nullable || typesFromCurrentAssembly.Any(o => o.Nullable);

      var allReferences = typesFromCurrentAssembly.Select(tr => ToReference(input, tr, ctx, overrideNullable));
      return $"z.union([{string.Join(", ", allReferences)}]){(overrideNullable ?? nullable ? ".nullable()" : "")}";
    }

    return ToReference(input, ps.Type, ctx, overrideNullable);
  }

  private string ToReference(ApiDefinitionModel input, TypeReference typeReference, AssemblyContext ctx, bool? overrideNullable = null)
  {
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? ".nullable()" : string.Empty;

    var preset = typeReference switch
    {
      { Name: "string" } => $"z.string(){n}",
      { Name: "date" } => $"z.string().datetime(){n}",
      { Name: "binary" } => $"z.string(){n}",
      { Name: "guid" } => $"z.string().uuid(){n}",
      { Name: "duration" } => $"z.string(){n}",
      { Name: "bool" } => $"z.boolean(){n}",
      { Name: "int32" or "int64" or "int16" } => $"z.number().int(){n}",
      { Name: "float32" or "float64" or "float128" } => $"z.number(){n}",

      { Name: "array", Arguments.Length: 1 } => $"{ToReference(input, typeReference.Arguments[0], ctx)}.array(){n}",
      // //{ Name: "map", Arguments: [var k, var v] } => $"{{[key:{ToReference(input, k, ctx, false)}]:{ToReference(input, v, ctx)}}}{n}",
      { Name: "map", Arguments.Length: 2 } => $"z.record({ToReference(input, typeReference.Arguments[1], ctx)}){n}",
      _ => null
    };

    if (preset != null) return preset;

    if (typeReference.Name.StartsWith("_"))
    {
      return $"{typeReference.Name[1..]}{n}";
    }

    // Object
    if (typeReference is { Name: "object" })
    {
      ctx.RegisterReferencedModule("EVA.Core");
      return ctx.AssemblyName == "EVA.Core" ? $"z.record(TAnyValue){n}" : $"z.record(EvaCore.TAnyValue){n}";
    }

    // Any
    if (typeReference is { Name: "any" })
    {
      ctx.RegisterReferencedModule("EVA.Core");
      return ctx.AssemblyName == "EVA.Core" ? $"TAnyValue{n}" : $"EvaCore.TAnyValue{n}";
    }

    // Apparently a type
    ctx.RegisterReferencedModule(input.Types[typeReference.Name].Assembly);
    if (!typeReference.Arguments.Any())
    {
      return TypescriptOutput.GetTypeRef(input, typeReference.Name, ctx);
    }
    else
    {
      var args = typeReference.Arguments.Select(a => ToReference(input, a, ctx));
      return $"{TypescriptOutput.GetTypeRef(input, typeReference.Name, ctx)}({string.Join(", ", args)})";
    }
  }
}