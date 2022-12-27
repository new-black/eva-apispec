using System.Collections.Immutable;
using System.Text;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;

public class TypescriptOutput : IOutput
{
  private readonly TypescriptOptions _options;

  public TypescriptOutput(TypescriptOptions options)
  {
    _options = options;
  }

  public string OutputPattern => null;

  public void FixOptions(GenerateOptions options)
  {
    // No-op
  }

  public async Task Write(ApiDefinitionModel input, string outputDirectory)
  {
    foreach (var group in input.GroupByAssembly())
    {
      var assemblyCtx = new AssemblyContext(group.Assembly);

      var sb = new IndentedStringBuilder(2);

      // Write the extenders
      WriteExtensions(input, sb, assemblyCtx);

      // Write namespace
      sb.WriteLine($"export namespace {FixNamespace(group.Assembly)} {{");
      sb.WriteIndented(o =>
      {
        // Preset types
        if (group.Assembly == "EVA.Core")
        {
          o.WriteLine();
          o.WriteLine("export type TAnyValue = string | number | boolean | Date | Array<TAnyValue> | { [key: string]: TAnyValue };");
        }

        // Write the errors
        o.WriteLine();
        var grouped = group.Errors.GroupByPrefix();
        WriteErrorGroup(grouped, o, "Errors");

        // Write the types
        WriteTypes(group, o, input, assemblyCtx);

        // Write the extenders
        foreach (var extender in assemblyCtx.ExtendersToGenerate)
        {
          o.WriteLine($"export interface {extender} {{ }}");
        }
      });
      sb.WriteLine("}");

      // Write the import statements
      var importsBuilder = new StringBuilder();
      foreach (var x in assemblyCtx.ReferencedModules)
      {
        importsBuilder.AppendLine($"import {{ {FixNamespace(x)} }} from '{GetModuleReference(x, _options.PackagePrefix)}';");
      }

      importsBuilder.AppendLine();
      importsBuilder.AppendLine(sb.ToString());

      await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{group.Assembly}.ts"), importsBuilder.ToString());
    }
  }

  private void WriteExtensions(ApiDefinitionModel input, IndentedStringBuilder sb, AssemblyContext ctx)
  {
    foreach (var (typeid, typespec) in input.Types)
    {
      if (typespec.Assembly == ctx.AssemblyName) continue;

      foreach (var (propname, propspec) in (typespec.Properties ?? ImmutableSortedDictionary<string, PropertySpecification>.Empty))
      {
        if (propspec.Type.Name != "option") continue;
        var typesInThisAssembly = propspec.Type.Arguments.Where(tr => input.Types[tr.Name].Assembly == ctx.AssemblyName).ToList();
        if (!typesInThisAssembly.Any()) continue;

        ctx.RegisterReferencedModule(typespec.Assembly);
        var extenderName = $"Extenders_{FixTypeName(input, typeid)}_{propname}";
        sb.WriteLine($"declare module '{GetModuleReference(typespec.Assembly, _options.PackagePrefix)}' {{");
        sb.WriteIndented(o =>
        {
          o.WriteLine($"export namespace {FixNamespace(typespec.Assembly)} {{");
          o.WriteIndented(o =>
          {
            o.WriteLine($"export interface {extenderName} {{");
            o.WriteIndented(o =>
            {
              foreach (var tita in typesInThisAssembly)
              {
                var typeRef = GetTypeRef(input, tita.Name, null);
                o.WriteLine($"{typeRef.Replace(".", string.Empty)}: {typeRef}{(tita.Nullable ? " | null" : string.Empty)};");
              }
            });
            o.WriteLine("}");
          });
          o.WriteLine("}");
        });
        sb.WriteLine("}");
      }
    }
  }

  private void WriteErrorGroup(ApiDefinitionModelExtensions.PrefixGroupedErrors errors, IndentedStringBuilder o, string prefix)
  {
    if (errors.Errors.Any())
    {
      o.WriteLine($"export const enum {prefix} {{");
      o.WriteIndented(o =>
      {
        foreach (var error in errors.Errors)
        {
          WriteComment(o, error.error.MessageWithEnhancedArguments());
          o.WriteLine($"{error.Name} = '{error.error.Name}',");
        }
      });
      o.WriteLine("}");
    }
    else if (errors.SubErrors.Any())
    {
      o.WriteLine($"export namespace {prefix} {{");
      o.WriteIndented(o =>
      {
        foreach (var (groupName, e) in errors.SubErrors)
        {
          WriteErrorGroup(e, o, groupName);
        }
      });
      o.WriteLine("}");
    }
  }

  private void WriteTypes(ApiDefinitionModelExtensions.GroupedApiDefinitionModel group, IndentedStringBuilder o, ApiDefinitionModel input, AssemblyContext ctx)
  {
    foreach (var (id, type) in group.Types)
    {
      if (type.EnumIsFlag.HasValue)
      {
        // We don't care about flag enums, there is no difference in TypeScript
        if (type.Description != null) WriteComment(o, type.Description);
        o.WriteLine($"export const enum {FixTypeName(input, id)} {{");
        o.WriteIndented(o =>
        {
          foreach (var (name, value) in type.EnumValues.ToTotals().OrderBy(x => x.Value))
          {
            o.WriteLine($"{name} = {value},");
          }
        });
        o.WriteLine("}");
        o.WriteLine();
      }
      else
      {
        var typeArgument = type.TypeArguments.Any() ? $"<{string.Join(", ", type.TypeArguments.Select(x => x[1..]))}>" : string.Empty;

        if (type.Description != null) WriteComment(o, type.Description);
        var fixedTypeName = FixTypeName(input, id);
        o.WriteLine($"export interface {fixedTypeName}{typeArgument} {{");
        o.WriteIndented(o =>
        {
          foreach (var (propName, propSpec) in type.Properties)
          {
            var propComment = new StringBuilder();
            if (propSpec.DataModelInformation is { Name: var dmi }) propComment.AppendLine($"Entity type: {dmi}");
            if (propSpec.Description != null) propComment.AppendLine(propSpec.Description);
            WriteComment(o, propComment.ToString());

            if (propSpec.Type.Nullable && !propSpec.Skippable)
            {
              o.WriteLine($"{propName}?: {ToReference(input, propSpec, propName, fixedTypeName, ctx, false)};");
            }
            else if (propSpec.Type.Nullable && propSpec.Skippable)
            {
              o.WriteLine($"{propName}: {ToReference(input, propSpec, propName, fixedTypeName, ctx)} | undefined;");
            }
            else if (!propSpec.Type.Nullable && !propSpec.Skippable)
            {
              o.WriteLine($"{propName}: {ToReference(input, propSpec, propName, fixedTypeName, ctx)};");
            }
            else if (!propSpec.Type.Nullable && propSpec.Skippable)
            {
              o.WriteLine($"{propName}?: {ToReference(input, propSpec, propName, fixedTypeName, ctx)};");
            }
          }
        });
        o.WriteLine("}");
        o.WriteLine();
      }
    }
  }

  private void WriteComment(IndentedStringBuilder o, string comment)
  {
    var c = comment.Trim();
    if (string.IsNullOrWhiteSpace(c)) return;
    o.WriteLine("/**");
    o.WriteLines(c, "* ");
    o.WriteLine("*/");
  }

  private string ToReference(ApiDefinitionModel input, PropertySpecification ps, string propName, string typeName, AssemblyContext ctx, bool? overrideNullable = null)
  {
    if (ps.Type.Name == "string" && ps.AllowedValues.Any())
    {
      return string.Join(" | ", ps.AllowedValues.Select(EscapeForString).Concat(overrideNullable ?? ps.Type.Nullable ? new[] { "null" } : Array.Empty<string>()));
    }

    // Option
    if (ps.Type is { Name: "option", Arguments: var options })
    {
      // Only use types from this assembly, and add an extender. This extender is patched later on.
      var typesFromCurrentAssembly = options.Where(o => input.Types[o.Name].Assembly == ctx.AssemblyName);
      var extenderName = $"Extenders_{typeName}_{propName}";
      var extenderRef = $"{extenderName}[keyof {extenderName}]";
      ctx.AddExtenderToGenerate(extenderName);
      var nullable = overrideNullable ?? ps.Type.Nullable || typesFromCurrentAssembly.Any(o => o.Nullable);

      var allReferences = typesFromCurrentAssembly.Select(tr => ToReference(input, tr, ctx, overrideNullable)).Concat(nullable ? new[] { "null", extenderRef } : new[] { extenderRef });
      return string.Join(" | ", allReferences);
    }

    return ToReference(input, ps.Type, ctx, overrideNullable);
  }

  private string ToReference(ApiDefinitionModel input, TypeReference typeReference, AssemblyContext ctx, bool? overrideNullable = null)
  {
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? " | null" : string.Empty;

    var preset = typeReference switch
    {
      { Name: "string" or "date" or "binary" or "guid" or "duration" } => $"string{n}",
      { Name: "bool" } => $"boolean{n}",
      { Name: "int32" or "int64" or "int16" or "float32" or "float64" or "float128" } => $"number{n}",
      //{ Name: "array", Arguments: [var a] } => $"{ToReference(input, a, ctx)}[]{n}",
      { Name: "array", Arguments.Length: 1 } => $"{ToReference(input, typeReference.Arguments[0], ctx)}[]{n}",
      //{ Name: ['_', .. var x] } => x,
      _ when typeReference.Name.StartsWith("_") => typeReference.Name[1..],
      //{ Name: "map", Arguments: [var k, var v] } => $"{{[key:{ToReference(input, k, ctx, false)}]:{ToReference(input, v, ctx)}}}{n}",
      { Name: "map", Arguments.Length: 2 } => $"{{[key:{ToReference(input, typeReference.Arguments[0], ctx, false)}]:{ToReference(input, typeReference.Arguments[1], ctx)}}}{n}",
      _ => null
    };

    if (preset != null) return preset;

    // Object
    if (typeReference is { Name: "object" })
    {
      ctx.RegisterReferencedModule("EVA.Core");
      return ctx.AssemblyName == "EVA.Core" ? $"Record<string, TAnyValue>{n}" : $"Record<string, EvaCore.TAnyValue>{n}";
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
      return GetTypeRef(input, typeReference.Name, ctx);
    }
    else
    {
      var args = typeReference.Arguments.Select(a => ToReference(input, a, ctx));
      return $"{GetTypeRef(input, typeReference.Name, ctx)}<{string.Join(", ", args)}>";
    }
  }

  /// <summary>
  /// Make a comment kinda string-safe.
  /// </summary>
  /// <param name="s"></param>
  /// <returns></returns>
  internal static string EscapeForString(string s)
  {
    return $"'{s.Replace("'", @"\'")}'";
  }

  /// <summary>
  /// Fixes the name of the namespace.
  /// </summary>
  /// <param name="s"></param>
  /// <returns></returns>
  internal static string FixNamespace(string s)
  {
    if (s.StartsWith("EVA.")) s = "Eva." + s[4..];
    return s.Replace(".", string.Empty);
  }

  /// <summary>
  /// Takes a type name, removes unnecessary module prefixes etc...
  /// </summary>
  /// <param name="input"></param>
  /// <param name="name"></param>
  /// <returns></returns>
  internal static string FixTypeName(ApiDefinitionModel input, string name)
  {
    var spec = input.Types[name];
    if (name.StartsWith(spec.Assembly + "."))
    {
      name = name[(spec.Assembly.Length + 1)..];
    }

    var idx = name.IndexOf('`');
    name = idx == -1 ? name : name[..idx];

    return name.Replace(".", string.Empty).Replace("+", "_");
  }

  /// <summary>
  /// Gets a reference to the given type. Prefixes with the correct assembly name if not in this assembly.
  /// </summary>
  /// <param name="input"></param>
  /// <param name="name"></param>
  /// <param name="currentAssembly"></param>
  /// <returns></returns>
  internal static string GetTypeRef(ApiDefinitionModel input, string name, AssemblyContext? ctx)
  {
    var spec = input.Types[name];
    var assembly = spec.Assembly == ctx?.AssemblyName ? string.Empty : $"{FixNamespace(spec.Assembly)}.";

    return $"{assembly}{FixTypeName(input, name)}";
  }

  internal static string GetModuleReference(string s, string? packagePrefix)
  {
    if (packagePrefix == null)
    {
      return $"./{s}";
    }
    else
    {
      if (s.StartsWith("EVA.")) s = s[4..];
      return $"{packagePrefix}{s.Replace(".", "-").ToLowerInvariant()}";
    }
  }
}