using System.Text;
using System.Text.RegularExpressions;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;

internal partial class TypescriptOutput : IOutput<TypescriptOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedTransformations => Array.Empty<string>();

  private const string AnyType = "TAnyValue";
  private const string IEvaServiceDefinition = "IEvaServiceDefinition";

  public async Task Write(OutputContext<TypescriptOptions> ctx)
  {
    var (input, options, writer, _) = ctx;

    foreach (var group in input.GroupByAssembly())
    {
      var assemblyCtx = new AssemblyContext(group.Assembly);

      // Write the types
      var o = new IndentedStringBuilder(2);

      // Write the extenders
      if (ctx.Options.Extenders) WriteExtensions(input, o, assemblyCtx, options);

      // Preset types
      if (group.Assembly == ApiSpecConsts.WellKnown.CoreAssembly)
      {
        o.WriteLine();
        o.WriteLine($"export type {AnyType} = string | number | boolean | Date | Array<{AnyType}> | {{ [key: string]: {AnyType} }};");
        o.WriteLine($"export interface {IEvaServiceDefinition} {{ name: string; path: string; request?: unknown; response?: unknown; }}");
        o.WriteLine("export function createServiceDefinition<SVC extends IEvaServiceDefinition>(service: new () => SVC): SVC { return new service(); }");
        o.WriteLine($"export const EVA_API_VERSION = {ctx.Input.ApiVersion};");
      }

      // Write the errors
      o.WriteLine();
      var grouped = group.Errors.GroupByPrefix();
      WriteErrorGroup(grouped, o, "Errors");

      // Write the types
      WriteTypes(group, o, input, assemblyCtx, ctx.Options);

      // Write the extenders
      if (ctx.Options.Extenders)
      {
        foreach (var extender in assemblyCtx.ExtendersToGenerate)
        {
          o.WriteLine($"export interface {extender} {{ }}");
        }
      }

      var assemblyName = AssemblyNameToPackageName(group.Assembly);
      await writer.WriteFileAsync($"{assemblyName}/src/{assemblyName}.ts", WriteImports(assemblyCtx, options, false) + o);

      // Write the services
      assemblyCtx = new AssemblyContext(group.Assembly);
      o = new IndentedStringBuilder(2);
      foreach (var service in group.Services)
      {
        o.WriteLine();
        assemblyCtx.RegisterReferencedType(ApiSpecConsts.WellKnown.CoreAssembly, IEvaServiceDefinition);
        o.WriteLine($"export class Svc{service.Name} implements {IEvaServiceDefinition}");
        using (o.BracedIndentation)
        {
          o.WriteLine($"name = {EscapeForString(service.Name)};");
          o.WriteLine($"path = {EscapeForString(service.Path)};");
          o.WriteLine($"request?: {TypeNameToTypescriptTypeName(assemblyCtx, input, service.RequestTypeID)};");
          o.WriteLine($"response?: {TypeNameToTypescriptTypeName(assemblyCtx, input, service.ResponseTypeID)};");
        }
      }

      await writer.WriteFileAsync($"{assemblyName}/src/{assemblyName}.services.ts", WriteImports(assemblyCtx, options, true) + o);
      await writer.WriteFileAsync($"{assemblyName}/src/index.ts", $"export * from './{assemblyName}.js';\nexport * from './{assemblyName}.services.js';");
    }
  }

  private static void WriteExtensions(ApiDefinitionModel input, IndentedStringBuilder o, AssemblyContext ctx, TypescriptOptions options)
  {
    foreach (var (typeid, typespec) in input.Types)
    {
      if (typespec.Assembly == ctx.AssemblyName) continue;

      foreach (var (propname, propspec) in typespec.Properties)
      {
        if (propspec.Type.Name != ApiSpecConsts.Specials.Option) continue;
        var typesInThisAssembly = propspec.Type.Arguments.Where(tr => input.Types[tr.Name].Assembly == ctx.AssemblyName).ToList();
        if (!typesInThisAssembly.Any()) continue;

        var extenderName = $"Extenders_{TypeNameToTypescriptTypeName(ctx, input, typeid, false)}_{propname}";
        o.WriteLine($"declare module '{DeterminePackageReference(typespec.Assembly, options.PackagePrefix)}'");
        using (o.BracedIndentation)
        {
          o.WriteLine($"export interface {extenderName}");
          using (o.BracedIndentation)
          {
            foreach (var tita in typesInThisAssembly)
            {
              var typeRef = ToReference(input, tita, ctx);
              o.WriteLine($"{typeRef.Replace(".", string.Empty)}: {typeRef}{(tita.Nullable ? " | null" : string.Empty)};");
            }
          }
        }
        o.WriteLine();
      }
    }
  }

  private static string WriteImports(AssemblyContext ctx, TypescriptOptions options, bool referenceSelf)
  {
    var o = new StringBuilder();
    foreach (var groupedReferences in ctx.ReferencedTypes.GroupBy(t => t.assembly))
    {
      if (groupedReferences.Key == ctx.AssemblyName && !referenceSelf) continue;

      o.AppendLine("import {");
      foreach (var reference in groupedReferences)
      {
        o.AppendLine($"  {reference.type},");
      }

      var moduleReference = groupedReferences.Key == ctx.AssemblyName
        ? $"./{AssemblyNameToPackageName(groupedReferences.Key)}.js"
        : DeterminePackageReference(groupedReferences.Key, options.PackagePrefix);

      o.AppendLine($"}} from '{moduleReference}';");
      o.AppendLine();
    }

    return o.ToString();
  }

  private static void WriteErrorGroup(ApiDefinitionModelExtensions.PrefixGroupedErrors errors, IndentedStringBuilder o, string prefix)
  {
    void write_error(ApiDefinitionModelExtensions.PrefixGroupedErrors errors, string prefix)
    {
      foreach (var error in errors.Errors)
      {
        WriteComment(o, error.error.MessageWithEnhancedArguments());
        o.WriteLine($"{prefix}{error.Name} = '{error.error.Name}',");
      }

      if (errors.SubErrors.Any())
      {
        foreach (var (groupName, e) in errors.SubErrors)
        {
          write_error(e, $"{prefix}{groupName}_");
        }
      }
    }

    if (errors.Errors.Any() || errors.SubErrors.Any())
    {
      o.WriteLine($"export const enum {prefix}");
      using (o.BracedIndentation)
      {
        write_error(errors, string.Empty);
      }
    }
  }

  private static void WriteTypes(ApiDefinitionModelExtensions.GroupedApiDefinitionModel group, IndentedStringBuilder o, ApiDefinitionModel input, AssemblyContext ctx, TypescriptOptions options)
  {
    foreach (var (id, type) in group.Types)
    {
      if (type.EnumIsFlag.HasValue)
      {
        // We don't care about flag enums, there is no difference in TypeScript
        if (type.Description != null) WriteComment(o, type.Description);
        o.WriteLine($"export const enum {TypeNameToTypescriptTypeName(ctx, input, id)} {{");
        using (o.Indentation)
        {
          foreach (var (name, value) in type.EnumValues.ToTotals().OrderBy(x => x.Value))
          {
            o.WriteLine($"{name} = {value},");
          }
        }

        o.WriteLine("}");
        o.WriteLine();
      }
      else
      {
        var typeArgument = type.TypeArguments.Any() ? $"<{string.Join(", ", type.TypeArguments.Select(x => x[1..]))}>" : string.Empty;

        if (type.Description != null) WriteComment(o, type.Description);
        var fixedTypeName = TypeNameToTypescriptTypeName(ctx, input, id);
        o.WriteLine($"export interface {fixedTypeName}{typeArgument} {{");
        using (o.Indentation)
        {
          foreach (var (propName, propSpec) in type.Properties)
          {
            var propComment = new StringBuilder();
            if (propSpec.DataModelInformation is { Name: var dmi }) propComment.AppendLine($"Entity type: {dmi}");
            if (propSpec.Description != null) propComment.AppendLine(propSpec.Description);
            WriteComment(o, propComment.ToString());

            if (propSpec.Type.Nullable && !propSpec.Skippable)
            {
              o.WriteLine($"{propName}?: {ToReference(input, propSpec, propName, fixedTypeName, ctx, options, false)};");
            }
            else if (propSpec.Type.Nullable && propSpec.Skippable)
            {
              o.WriteLine($"{propName}: {ToReference(input, propSpec, propName, fixedTypeName, ctx, options)} | undefined;");
            }
            else if (!propSpec.Type.Nullable && !propSpec.Skippable)
            {
              o.WriteLine($"{propName}: {ToReference(input, propSpec, propName, fixedTypeName, ctx, options)};");
            }
            else if (!propSpec.Type.Nullable && propSpec.Skippable)
            {
              o.WriteLine($"{propName}?: {ToReference(input, propSpec, propName, fixedTypeName, ctx, options)};");
            }
          }
        }

        o.WriteLine("}");
        o.WriteLine();
      }
    }
  }



  private static string ToReference(ApiDefinitionModel input, PropertySpecification ps, string propName, string typeName, AssemblyContext ctx, TypescriptOptions o, bool? overrideNullable = null)
  {
    if (ps.Type.Name == ApiSpecConsts.String && ps.AllowedValues.Any())
    {
      return string.Join(" | ", ps.AllowedValues.Select(EscapeForString).Concat(overrideNullable ?? ps.Type.Nullable ? new[] { "null" } : Array.Empty<string>()));
    }

    // Option
    if (ps.Type is { Name: ApiSpecConsts.Specials.Option, Arguments: var options })
    {
      // Only use types from this assembly, and add an extender. This extender is patched later on.
      var typesFromCurrentAssembly = options.Where(o => input.Types[o.Name].Assembly == ctx.AssemblyName).ToList();
      var nullable = overrideNullable ?? ps.Type.Nullable || typesFromCurrentAssembly.Any(o => o.Nullable);

      var extra = new List<string?>();
      if (nullable) extra.Add("null");
      if (o.Extenders)
      {
        var extenderName = $"Extenders_{typeName}_{propName}";
        var extenderRef = $"{extenderName}[keyof {extenderName}]";
        ctx.AddExtenderToGenerate(extenderName);
        extra.Add(extenderRef);
      }

      var allReferences = (typesFromCurrentAssembly.Any() ? typesFromCurrentAssembly :[ps.Type.Shared])
        .Select(tr => ToReference(input, tr, ctx, overrideNullable)).Concat(extra);
      return string.Join(" | ", allReferences);
    }

    return ToReference(input, ps.Type, ctx, overrideNullable);
  }

  private static string ToReference(ApiDefinitionModel input, TypeReference typeReference, AssemblyContext ctx, bool? overrideNullable = null)
  {
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? " | null" : string.Empty;

    var preset = typeReference switch
    {
      { Name: ApiSpecConsts.ID } => $"number{n}",
      { Name: ApiSpecConsts.String or ApiSpecConsts.Date or ApiSpecConsts.Binary or ApiSpecConsts.Guid or ApiSpecConsts.Duration } => $"string{n}",
      { Name: ApiSpecConsts.Bool } => $"boolean{n}",
      { Name: ApiSpecConsts.Int32 or ApiSpecConsts.Int64 or ApiSpecConsts.Int16 or ApiSpecConsts.Float32 or ApiSpecConsts.Float64 or ApiSpecConsts.Float128 } => $"number{n}",
      { Name: ApiSpecConsts.Specials.Array, Arguments.Length: 1 } => $"{ToReference(input, typeReference.Arguments[0], ctx)}[]{n}",
      _ when typeReference.Name.StartsWith("_") => typeReference.Name[1..],
      // Key will always be a string
      { Name: ApiSpecConsts.Specials.Map, Arguments.Length: 2 } => $"{{[key:string]:{ToReference(input, typeReference.Arguments[1], ctx)}}}{n}",
      _ => null
    };

    if (preset != null) return preset;

    // Object
    if (typeReference is { Name: ApiSpecConsts.Object })
    {
      ctx.RegisterReferencedType(ApiSpecConsts.WellKnown.CoreAssembly, AnyType);
      return $"Record<string, {AnyType}>{n}";
    }

    // Any
    if (typeReference is { Name: ApiSpecConsts.Any })
    {
      ctx.RegisterReferencedType(ApiSpecConsts.WellKnown.CoreAssembly, AnyType);
      return $"{AnyType}{n}";
    }

    // Apparently a type
    if (!typeReference.Arguments.Any())
    {
      return TypeNameToTypescriptTypeName(ctx, input, typeReference.Name);
    }

    var args = new List<string>();
    foreach (var a in typeReference.Arguments)
    {
      args.Add(ToReference(input, a, ctx));
    }

    return $"{TypeNameToTypescriptTypeName(ctx, input, typeReference.Name)}<{string.Join(", ", args)}>";
  }





  private static void WriteComment(IndentedStringBuilder o, string comment)
  {
    var c = comment.Trim();
    if (string.IsNullOrWhiteSpace(c)) return;
    o.WriteLine("/**");
    o.WriteLines(c, "* ");
    o.WriteLine("*/");
  }

  internal static string EscapeForString(string s)
  {
    return $"'{s.Replace("'", @"\'")}'";
  }

  internal static string TypeNameToTypescriptTypeName(AssemblyContext ctx, ApiDefinitionModel input, string name, bool addReference = true)
  {
    // For service types
    {
      if (input.Services.Any(s => s.RequestTypeID == name || s.ResponseTypeID == name))
      {
        var idx = name.IndexOf('`');
        var spec = input.Types[name];
        name = idx == -1 ? name : name[..idx];

        idx = name.LastIndexOf('.');
        name = idx == -1 ? name : name[(idx + 1)..];

        ctx.RegisterReferencedType(spec.Assembly, name);
        return name;
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
      if (addReference) ctx.RegisterReferencedType(spec.Assembly, result);
      return result;
    }
  }

  internal static string DeterminePackageReference(string assemblyName, string? packagePrefix)
  {
    assemblyName = AssemblyNameToPackageName(assemblyName);
    if (packagePrefix == null) return $"../{assemblyName}";
    var trimmedPrefix = packagePrefix.TrimStart('\\');
    return $"{trimmedPrefix}{assemblyName}";
  }

  private static string AssemblyNameToPackageName(string a)
  {
    if (a.StartsWith("EVA.")) a = $"eva-services-{a[4..]}";
    var result = a.Replace(".", "-");

    var parts = result.Split('-');

    // We always lowercase the first letter of each part
    // If the part contains an uppercase letter, we replace it with a '-' and lowercase it
    for (var i = 0; i < parts.Length; i++)
    {
      var part = parts[i];
      if (part.Length == 0) continue;

      // if part is totally uppercase, we just lowercase it
      if (part.ToUpperInvariant() == part)
      {
        parts[i] = part.ToLower();
        continue;
      }

      // If part ends with upper case letters, we lowercase all of these
      var numUpperCaseLettersAtEnd = part.Reverse().TakeWhile(char.IsUpper).Count();
      if (numUpperCaseLettersAtEnd > 0)
      {
        part = part[..^numUpperCaseLettersAtEnd] + part[^numUpperCaseLettersAtEnd..].ToLower();
      }

      // Camel-case to kebab-case
      parts[i] = part[0].ToString().ToLower() + _pascalCaseToKebabCaseRegex.Replace(part[1..], "-$1").ToLower();
    }

    return string.Join("-", parts);
  }

  private static readonly Regex _pascalCaseToKebabCaseRegex = PascalCaseToKebabCaseRegex();

  [GeneratedRegex(@"([A-Z])", RegexOptions.Compiled)]
  private static partial Regex PascalCaseToKebabCaseRegex();
}