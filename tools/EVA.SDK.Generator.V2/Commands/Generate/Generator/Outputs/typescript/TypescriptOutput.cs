using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.typescript;

public class TypescriptOutput : IOutput
{
  private readonly TypescriptOptions _options;

  public TypescriptOutput(TypescriptOptions options)
  {
    _options = options;
  }

  public void FixOptions(GenerateOptions options)
  {
    // No-op
  }

  public async Task Write(ApiDefinitionModel input, string outputDirectory)
  {
    foreach (var group in input.GroupByAssembly())
    {
      var sb = new IndentedStringBuilder(2);
      sb.WriteLine($"declare module {group.Assembly} {{");
      sb.WriteIndentend(o =>
      {
        o.WriteLine();
        var grouped = group.Errors.GroupByPrefix();
        WriteErrorGroup(grouped, o, "Errors");

        foreach (var (id, type) in group.Types)
        {
          if (type.EnumIsFlag.HasValue)
          {
            if (type.Description != null) WriteComment(o, type.Description);
            o.WriteLine($"export const enum {type.TypeName} {{");
            o.WriteIndentend(o =>
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
            o.WriteLine($"export interface {FixTypeName(type.TypeName)}{typeArgument} {{");
            o.WriteIndentend(o =>
            {
              foreach (var (propName, propSpec) in (type.Properties ?? ImmutableSortedDictionary<string, PropertySpecification>.Empty))
              {
                var propComment = new StringBuilder();
                if (propSpec.DataModelInformation is { Name: var dmi }) propComment.AppendLine($"Entity type: {dmi}");
                if (propSpec.Description != null) propComment.AppendLine(propSpec.Description);
                WriteComment(o, propComment.ToString());

                if (propSpec.Type.Nullable && !propSpec.Skippable)
                {
                  o.WriteLine($"{propName}?: {ToReference(input, propSpec, false)};");
                }
                else if (propSpec.Type.Nullable && propSpec.Skippable)
                {
                  o.WriteLine($"{propName}: {ToReference(input, propSpec)} | undefined;");
                }
                else if (!propSpec.Type.Nullable && !propSpec.Skippable)
                {
                  o.WriteLine($"{propName}: {ToReference(input, propSpec)};");
                }
                else if (!propSpec.Type.Nullable && propSpec.Skippable)
                {
                  o.WriteLine($"{propName}?: {ToReference(input, propSpec)};");
                }
              }
            });
            o.WriteLine("}");
            o.WriteLine();
          }
        }
      });
      sb.WriteLine("}");

      await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{group.Assembly}.ts"), sb.ToString());
    }
  }

  private void WriteErrorGroup(ApiDefinitionModelExtensions.PrefixGroupedErrors errors, IndentedStringBuilder o, string prefix)
  {
    if (errors.Errors.Any())
    {
      o.WriteLine($"export const enum {prefix} {{");
      o.WriteIndentend(o =>
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
      o.WriteIndentend(o =>
      {
        foreach (var (groupName, e) in errors.SubErrors)
        {
          WriteErrorGroup(e, o, groupName);
        }
      });
      o.WriteLine("}");
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

  private string ToReference(ApiDefinitionModel input, PropertySpecification ps, bool? overrideNullable = null)
  {
    if (ps.Type.Name == "string" && ps.AllowedValues.Any())
    {
      return string.Join(" | ", ps.AllowedValues.Select(EscapeForString).Concat(overrideNullable ?? ps.Type.Nullable ? new[] { "null" } : Array.Empty<string>()));
    }

    return ToReference(input, ps.Type, overrideNullable);
  }

  private string ToReference(ApiDefinitionModel input, TypeReference typeReference, bool? overrideNullable = null)
  {
    var nullable = overrideNullable ?? typeReference.Nullable;
    var n = nullable ? " | null" : string.Empty;
    var preset = typeReference switch
    {
      { Name: "string" or "date" or "binary" or "guid" or "duration" } => $"string{n}",
      { Name: "bool" } => $"boolean{n}",
      { Name: "int32" or "int64" or "int16" or "float32" or "float64" or "float128" } => $"number{n}",
      { Name: "array", Arguments: [var a] } => $"{ToReference(input, a)}[]{n}",
      { Name: "object" } => $"{{[key:string]:any}}{n}",
      { Name: "any" } => "any",
      { Name: ['_', .. var x] } => x,
      { Name: "map", Arguments: [var k, var v] } => $"{{[key:{ToReference(input, k,false)}]:{ToReference(input, v)}}}{n}",
      { Name: "option", Arguments: var args } => string.Join(
        " | ",
        args.Select(a => ToReference(input, a, false))
          .Concat(nullable || args.Any(a => a.Nullable) ? new[] { "null" } : Array.Empty<string>())
      ),
      _ => null
    };
    if (preset != null) return preset;

    // Apparently a type
    if (!typeReference.Arguments.Any())
    {
      var spec = input.Types[typeReference.Name];
      return $"{spec.Assembly}.{FixTypeName(spec.TypeName)}";
    }
    else
    {
      var spec = input.Types[typeReference.Name];
      var args = typeReference.Arguments.Select(a => ToReference(input, a));
      return $"{spec.Assembly}.{FixTypeName(spec.TypeName)}<{string.Join(", ", args)}>";
    }
  }

  private static string EscapeForString(string s)
  {
    return $"'{s.Replace("'", @"\'")}'";
  }

  private static string FixTypeName(string s)
  {
    var idx = s.IndexOf('`');
    return idx == -1 ? s : s[..idx];
  }
}