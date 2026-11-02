using System.Text;
using System.Text.RegularExpressions;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Outputs.typescript;

internal partial class TypescriptOutput : IApiOutput<TypescriptOptions>
{
  public string? OutputPattern => null;

  public bool GetForcedTransformations(TypescriptOptions _, IApiTransform x) => false;

  private const string AnyType = "TAnyValue";
  private const string IEvaServiceDefinition = "IEvaServiceDefinition";
  private const string CoreAssembly = "EVA.Core";
  private const string IProductSearchItem = "EVA.Core.Search.IProductSearchItem";

  private sealed class State(EvaApi api, TypescriptOptions options)
  {
    internal EvaApi Api { get; } = api;
    internal TypescriptOptions Options { get; } = options;

    /// <summary>
    /// The request and response types of the services.
    /// </summary>
    internal HashSet<string> ServiceTypes { get; } = api.Services.SelectMany(s => new[] { s.RequestTypeID, s.ResponseTypeID }).ToHashSet();
  }

  public async Task Write(ApiOutputContext<TypescriptOptions> ctx)
  {
    var (api, options, writer, _) = ctx;
    var state = new State(api, options);

    var assemblyDependencies = BuildAssemblyDependencies(api);

    foreach (var group in api.GroupByAssembly())
    {
      var assemblyName = AssemblyNameToPackageName(group.Assembly);
      var assemblyCtx = new AssemblyContext(group.Assembly, assemblyDependencies.GetValueOrDefault(group.Assembly, []));

      // Write the extenders
      var hasExtensions = false;
      if (options.Extenders)
      {
        var eo = new IndentedStringBuilder(2);
        WriteExtensions(state, assemblyDependencies, eo, assemblyCtx);

        var content = eo.ToString();
        if (!string.IsNullOrWhiteSpace(content))
        {
          hasExtensions = true;
          await writer.WriteFileAsync($"{assemblyName}/src/{assemblyName}.ext.ts", WriteImports(assemblyCtx, options, true) + content);
          assemblyCtx.ResetReferences();
        }
      }

      var o = new IndentedStringBuilder(2);

      // Preset types
      if (group.Assembly == CoreAssembly)
      {
        o.WriteLine();
        o.WriteLine($"export type {AnyType} = string | number | boolean | Array<{AnyType}> | {{ [key: string]: {AnyType} }};");
        o.WriteLine($"export interface {IEvaServiceDefinition} {{ name: string; path: string; request?: unknown; response?: unknown; }}");
        o.WriteLine($"export function createServiceDefinition<SVC extends {IEvaServiceDefinition}>(service: new () => SVC): SVC {{ return new service(); }}");
        o.WriteLine($"export const EVA_API_VERSION = {api.ApiVersion};");
      }

      // Write the errors
      o.WriteLine();
      var grouped = group.Errors.GroupByPrefix();
      WriteErrorGroup(state, grouped, o, "Errors");

      // Write the types
      WriteTypes(state, group, o, assemblyCtx);

      // Write the extenders
      if (options.Extenders)
      {
        foreach (var extender in assemblyCtx.ExtendersToGenerate)
        {
          o.WriteLine($"export interface {extender}<ID_TYPE> {{ }}");
        }
      }

      // We reset the assembly context so we don't get all the references
      await writer.WriteFileAsync($"{assemblyName}/src/{assemblyName}.ts", WriteImports(assemblyCtx, options, false) + o);
      assemblyCtx.ResetReferences();

      // Write the services
      o = new IndentedStringBuilder(2);
      foreach (var service in group.Services)
      {
        o.WriteLine();
        assemblyCtx.RegisterReferencedType(CoreAssembly, IEvaServiceDefinition);
        var flexIDSuffix = options.FlexibleIDs ? "<ID_TYPE>" : string.Empty;
        o.WriteLine($"export class Svc{service.Name}{(options.FlexibleIDs ? "<ID_TYPE = string>" : string.Empty)} implements {IEvaServiceDefinition}");
        using (o.BracedIndentation)
        {
          o.WriteLine($"name = {EscapeForString(service.Name)};");
          o.WriteLine($"path = {EscapeForString(service.Path)};");
          o.WriteLine($"request?: {TypeNameToTypescriptTypeName(state, assemblyCtx, service.RequestTypeID)}{flexIDSuffix};");
          o.WriteLine($"response?: {TypeNameToTypescriptTypeName(state, assemblyCtx, service.ResponseTypeID)}{flexIDSuffix};");
        }
      }

      await writer.WriteFileAsync($"{assemblyName}/src/{assemblyName}.services.ts", WriteImports(assemblyCtx, options, true) + o);

      var indexContent = $"export * from './{assemblyName}.js';\nexport * from './{assemblyName}.services.js';";
      if (hasExtensions)
      {
        indexContent += $"\nexport * from './{assemblyName}.ext.js';";
      }

      // Write the index file
      await writer.WriteFileAsync($"{assemblyName}/src/index.ts", indexContent);
    }
  }

  private static void WriteExtensions(State state, Dictionary<string, HashSet<string>> assemblyDependencies, IndentedStringBuilder o, AssemblyContext ctx)
  {
    var flexIDSuffix = state.Options.FlexibleIDs ? "<ID_TYPE>" : string.Empty;
    var ocontent = new IndentedStringBuilder(2);
    var touchedPackages = new HashSet<string>();

    foreach (var (typeid, type) in state.Api.Types)
    {
      if (type.Assembly == ctx.AssemblyName) continue;

      foreach (var (propname, property) in type.Properties)
      {
        if (property.Type is not OptionRef option) continue;
        var typesInThisAssembly = option.Options.Where(tr => state.Api.GetType(tr.TypeID).Assembly == ctx.AssemblyName).ToList();
        if (!typesInThisAssembly.Any()) continue;

        if (assemblyDependencies.TryGetValue(type.Assembly, out var dependencies) && dependencies.Contains(ctx.AssemblyName))
        {
          // We don't need an extender for this relation because there is a regular dependency to fix this
          continue;
        }

        var extenderName = $"Extenders_{TypeNameToTypescriptTypeName(state, ctx, typeid, false)}_{propname}";
        var packageName = DeterminePackageReference(type.Assembly, state.Options.PackagePrefix);
        touchedPackages.Add(packageName);
        ocontent.WriteLine($"declare module '{packageName}'");
        using (ocontent.BracedIndentation)
        {
          ocontent.WriteLine($"export interface {extenderName}{flexIDSuffix}");
          using (ocontent.BracedIndentation)
          {
            foreach (var tita in typesInThisAssembly)
            {
              var typeRef = ToReference(state, tita, ctx, false);
              ocontent.WriteLine($"{typeRef.Replace(".", string.Empty).Replace(", ID_TYPE>", string.Empty).Replace("<ID_TYPE>", string.Empty)}: {typeRef};");
            }
          }
        }

        ocontent.WriteLine();
      }
    }

    foreach (var package in touchedPackages)
    {
      o.WriteLine($"import '{package}';");
    }

    o.WriteLine();
    o.Write(ocontent.ToString());
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

  private static void WriteErrorGroup(State state, EvaErrorExtensions.PrefixGroupedErrors errors, IndentedStringBuilder o, string prefix)
  {
    void write_error(EvaErrorExtensions.PrefixGroupedErrors errors, string prefix)
    {
      foreach (var error in errors.Errors)
      {
        WriteComment(o, error.Error.MessageWithEnhancedArguments(state.Api));
        o.WriteLine($"{prefix}{error.Name} = '{error.Error.Name}',");
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
      if (state.Options.ConstEnums)
      {
        o.WriteLine($"export const enum {prefix}");
      }
      else
      {
        o.WriteLine($"export enum {prefix}");
      }

      using (o.BracedIndentation)
      {
        write_error(errors, string.Empty);
      }
    }
  }

  private static void WriteTypes(State state, EvaApi.GroupedApi group, IndentedStringBuilder o, AssemblyContext ctx)
  {
    var options = state.Options;

    foreach (var (id, type) in group.Types)
    {
      if (type.IsEnum)
      {
        // We don't care about flag enums, there is no difference in TypeScript
        if (type.Description != null) WriteComment(o, type.Description);

        if (options.ConstEnums)
        {
          o.WriteLine($"export const enum {TypeNameToTypescriptTypeName(state, ctx, id)} {{");
        }
        else
        {
          o.WriteLine($"export enum {TypeNameToTypescriptTypeName(state, ctx, id)} {{");
        }

        using (o.Indentation)
        {
          foreach (var value in type.EnumValuesByValue)
          {
            o.WriteLine($"{value.Name} = {value.Value},");
          }
        }

        o.WriteLine("}");
        o.WriteLine();
      }
      else if (id == IProductSearchItem)
      {
        var fixedTypeName = TypeNameToTypescriptTypeName(state, ctx, id);
        o.WriteLine($"export interface {fixedTypeName}{(options.FlexibleIDs ? "<ID_TYPE>" : string.Empty)} extends Record<string, {AnyType} | null> {{");
        using (o.Indentation)
        {
        }

        o.WriteLine("}");
        o.WriteLine();
      }
      else
      {
        var typeParameters = type.TypeParameters;
        var typeArgument =
          options.FlexibleIDs
            ? (typeParameters.Any() ? $"<{string.Join(", ", typeParameters)}, ID_TYPE = string>" : "<ID_TYPE = string>")
            : (typeParameters.Any() ? $"<{string.Join(", ", typeParameters)}>" : string.Empty);
        var basetype = type.Extends == null ? string.Empty : $" extends {ToReference(state, type.Extends, ctx)}";

        if (type.Description != null) WriteComment(o, type.Description);
        var fixedTypeName = TypeNameToTypescriptTypeName(state, ctx, id);
        o.WriteLine($"export interface {fixedTypeName}{typeArgument}{basetype} {{");
        using (o.Indentation)
        {
          foreach (var (propName, property) in type.Properties)
          {
            var propertyType = property.Type;
            var propComment = new StringBuilder();
            if (property.Entity is { Name: var entity }) propComment.AppendLine($"Entity type: {entity}");
            if (property.Description != null) propComment.AppendLine(property.Description);
            WriteComment(o, propComment.ToString());

            if (propertyType.Nullable && !property.Skippable)
            {
              o.WriteLine($"{propName}?: {ToReference(state, property, propName, fixedTypeName, ctx, false)};");
            }
            else if (propertyType.Nullable && property.Skippable)
            {
              o.WriteLine($"{propName}?: {ToReference(state, property, propName, fixedTypeName, ctx)};");
            }
            else if (!propertyType.Nullable && !property.Skippable)
            {
              // "primitive values" are always optional because they have a default value that is not `null`
              // we only want to do this in request-only types
              if (type.Usage.request && propertyType is PrimitiveRef { Kind: PrimitiveKind.Bool or PrimitiveKind.Int32 or PrimitiveKind.Int64 })
              {
                o.WriteLine($"{propName}?: {ToReference(state, property, propName, fixedTypeName, ctx)};");
              }
              else if (type.Usage.request && propertyType is NamedRef named && state.Api.TryGetType(named.TypeID) is { IsEnum: true })
              {
                // While an enum is not a primitive, it is still optional
                o.WriteLine($"{propName}?: {ToReference(state, property, propName, fixedTypeName, ctx)};");
              }
              else
              {
                o.WriteLine($"{propName}: {ToReference(state, property, propName, fixedTypeName, ctx)};");
              }
            }
            else if (!propertyType.Nullable && property.Skippable)
            {
              o.WriteLine($"{propName}?: {ToReference(state, property, propName, fixedTypeName, ctx)};");
            }
          }
        }

        o.WriteLine("}");
        o.WriteLine();
      }
    }
  }

  private static string ToReference(State state, EvaProperty property, string propName, string typeName, AssemblyContext ctx, bool? overrideNullable = null)
  {
    var type = property.Type;

    if (type is PrimitiveRef { Kind: PrimitiveKind.String } && property.AllowedValues.Any())
    {
      return string.Join(" | ", property.AllowedValues.Select(EscapeForString).Concat(overrideNullable ?? type.Nullable ? ["null"] : []));
    }

    // Option
    if (type is OptionRef option)
    {
      // Only use types from this assembly, and add an extender. This extender is patched later on.
      var flexIDSuffix = state.Options.FlexibleIDs ? "<ID_TYPE>" : string.Empty;
      var referencableTypes = option.Options.Where(o =>
      {
        var typeAssembly = state.Api.GetType(o.TypeID).Assembly;
        return typeAssembly == ctx.AssemblyName || ctx.Dependencies.Contains(typeAssembly);
      }).ToList();

      var nullable = overrideNullable ?? type.Nullable || referencableTypes.Any(o => o.Nullable);

      var extra = new List<string?>();
      if (nullable) extra.Add("null");
      if (state.Options.Extenders)
      {
        var extenderName = $"Extenders_{typeName}_{propName}";
        var extenderRef = $"{extenderName}{flexIDSuffix}[keyof {extenderName}{flexIDSuffix}]";
        ctx.AddExtenderToGenerate(extenderName);
        extra.Add(extenderRef);
      }

      var allReferences = (referencableTypes.Any() ? referencableTypes.Cast<TypeRef>() : [option.Shared])
        .Select(tr => ToReference(state, tr, ctx, overrideNullable)).Concat(extra);
      return string.Join(" | ", allReferences);
    }

    return ToReference(state, type, ctx, overrideNullable);
  }

  private static string ToReference(State state, TypeRef typeReference, AssemblyContext ctx, bool? overrideNullable = null)
  {
    var options = state.Options;
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? " | null" : string.Empty;

    var preset = typeReference switch
    {
      PrimitiveRef { Kind: PrimitiveKind.ID or PrimitiveKind.String or PrimitiveKind.Date or PrimitiveKind.Binary or PrimitiveKind.Guid or PrimitiveKind.Duration } => $"string{n}",
      PrimitiveRef { Kind: PrimitiveKind.Bool } => $"boolean{n}",
      PrimitiveRef { Kind: PrimitiveKind.Int32 or PrimitiveKind.Int64 or PrimitiveKind.Int16 or PrimitiveKind.Float32 or PrimitiveKind.Float64 or PrimitiveKind.Decimal } => $"number{n}",
      ArrayRef array => $"{ToReference(state, array.Items, ctx, false)}[]{n}",
      TypeParameterRef parameter => parameter.Name,
      // Key will always be a string
      MapRef map => $"Record<string,{ToReference(state, map.Value, ctx)}>{n}",
      _ => null
    };

    if (preset != null) return preset;

    // Object
    if (typeReference is PrimitiveRef { Kind: PrimitiveKind.Object })
    {
      ctx.RegisterReferencedType(CoreAssembly, AnyType);
      return $"Record<string, {AnyType}>{n}";
    }

    // Any
    if (typeReference is PrimitiveRef { Kind: PrimitiveKind.Any })
    {
      ctx.RegisterReferencedType(CoreAssembly, AnyType);
      return $"{AnyType}{n}";
    }

    var named = (NamedRef)typeReference;

    // Apparently a type
    if (!named.Arguments.Any())
    {
      if (state.Api.TryGetType(named.TypeID)?.IsEnum ?? true)
      {
        return TypeNameToTypescriptTypeName(state, ctx, named.TypeID);
      }

      return TypeNameToTypescriptTypeName(state, ctx, named.TypeID) + (options.FlexibleIDs ? "<ID_TYPE>" : string.Empty);
    }

    var args = new List<string>();
    foreach (var a in named.Arguments)
    {
      args.Add(ToReference(state, a, ctx));
    }

    return $"{TypeNameToTypescriptTypeName(state, ctx, named.TypeID)}<{string.Join(", ", args)}{(options.FlexibleIDs ? ", ID_TYPE" : "")}>";
  }

  private static void WriteComment(IndentedStringBuilder o, string comment)
  {
    var c = comment.Trim();
    if (string.IsNullOrWhiteSpace(c)) return;
    o.WriteLine("/**");
    o.WriteLines(c, "* ");
    o.WriteLine("*/");
  }

  private static string EscapeForString(string s)
  {
    return $"'{s.Replace("'", @"\'")}'";
  }

  private static string TypeNameToTypescriptTypeName(State state, AssemblyContext ctx, string name, bool addReference = true)
  {
    var spec = state.Api.GetType(name);

    // For service types
    if (state.ServiceTypes.Contains(name))
    {
      var idx = name.IndexOf('`');
      name = idx == -1 ? name : name[..idx];

      idx = name.LastIndexOf('.');
      name = idx == -1 ? name : name[(idx + 1)..];

      if (addReference) ctx.RegisterReferencedType(spec.Assembly, name);
      return name;
    }

    // Other types
    {
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

  private static string DeterminePackageReference(string assemblyName, string? packagePrefix)
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
      parts[i] = part[0].ToString().ToLower() + PascalCaseToKebabCaseRegex().Replace(part[1..], "-$1").ToLower();
    }

    return string.Join("-", parts);
  }

  [GeneratedRegex(@"([A-Z])", RegexOptions.Compiled)]
  private static partial Regex PascalCaseToKebabCaseRegex();

  /// <summary>
  /// The assemblies each assembly refers to, through the types of its types. The implementations of options don't count.
  /// </summary>
  private static Dictionary<string, HashSet<string>> BuildAssemblyDependencies(EvaApi api)
  {
    var result = new Dictionary<string, HashSet<string>>();

    foreach (var (_, type) in api.Types)
    {
      foreach (var dependency in TypeDependencies(type))
      {
        var dependencyType = api.GetType(dependency);
        if (dependencyType.Assembly == type.Assembly) continue;

        if (!result.TryGetValue(type.Assembly, out var dependencies))
        {
          dependencies = new HashSet<string>();
          result[type.Assembly] = dependencies;
        }

        dependencies.Add(dependencyType.Assembly);
      }
    }

    return result;
  }

  private static IEnumerable<string> TypeDependencies(EvaType type)
  {
    IEnumerable<string> Names(TypeRef reference) => reference switch
    {
      NamedRef n => n.Arguments.SelectMany(Names).Prepend(n.TypeID),
      ArrayRef a => Names(a.Items),
      MapRef m => Names(m.Key).Concat(Names(m.Value)),
      OptionRef o => Names(o.Shared),
      _ => []
    };

    return (type.Extends == null ? [] : Names(type.Extends))
      .Concat(type.Properties.Values.SelectMany(p => Names(p.Type)))
      .Concat(type.Parent == null ? [] : [type.Parent.ID])
      .Distinct();
  }
}
