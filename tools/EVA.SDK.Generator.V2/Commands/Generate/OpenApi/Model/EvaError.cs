using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;

/// <summary>
/// A value in the message of an error.
/// </summary>
/// <param name="Name">The name of the value, if it has one.</param>
/// <param name="Schema">The schema of the value.</param>
/// <param name="TypeName">The name of the type of the value, see <see cref="TypeRefExtensions.SpecName"/>. Errors don't keep the types of
/// their values in the API, so a schema may refer to a type that is no longer there.</param>
internal sealed record EvaErrorArgument(string? Name, IOpenApiSchema Schema, string TypeName);

/// <summary>
/// An error services can return.
/// </summary>
internal sealed class EvaError(string name, string message, string assembly, List<EvaErrorArgument> arguments)
{
  internal string Name { get; } = name;
  internal string Message { get; } = message;
  internal string Assembly { get; set; } = assembly;

  /// <summary>
  /// The values of the placeholders in <see cref="Message"/>, in order.
  /// </summary>
  internal List<EvaErrorArgument> Arguments { get; } = arguments;

  internal static EvaError Read(JsonNode json, EvaApi api) => new(
    json["name"]!.GetValue<string>(),
    json["message"]!.GetValue<string>(),
    json["assembly"]!.GetValue<string>(),
    json["arguments"]!.AsArray().Select(a =>
    {
      var schema = Schemas.Read(a!["schema"]!, api.Document);
      return new EvaErrorArgument(a["name"]?.GetValue<string>(), schema, api.ToTypeRef(schema).SpecName());
    }).ToList());

  public override string ToString() => Name;
}

internal static class EvaErrorExtensions
{
  internal record PrefixGroupedErrors(List<(string Name, EvaError Error)> Errors, ImmutableSortedDictionary<string, PrefixGroupedErrors> SubErrors);

  /// <summary>
  /// The message with each placeholder replaced by the name (or position) and type of its argument.
  /// </summary>
  internal static string MessageWithEnhancedArguments(this EvaError error)
  {
    if (!error.Arguments.Any()) return error.Message;

    var i = 0;
    return string.Format(error.Message, error.Arguments.Select(object (a) => $"{{{a.Name ?? i++.ToString()}:{a.TypeName}}}").ToArray());
  }

  /// <summary>
  /// Groups errors by the parts of their names, eg Orders:Payments:Failed is grouped in Orders and then in Payments.
  /// </summary>
  internal static PrefixGroupedErrors GroupByPrefix(this IEnumerable<EvaError> errors, int skip = 0)
  {
    var rootErrors = new List<(string Name, EvaError error)>();
    var subErrors = new SortedDictionary<string, List<EvaError>>();

    foreach (var e in errors)
    {
      var name = e.Name;
      var idx = name.IndexOf(':', skip);
      if (idx == -1)
      {
        rootErrors.Add((name[skip..], e));
      }
      else
      {
        var category = name[skip..idx];
        if (!subErrors.TryGetValue(category, out var l)) subErrors[category] = l = new List<EvaError>();
        l.Add(e);
      }
    }

    return new PrefixGroupedErrors(
      rootErrors,
      subErrors.ToImmutableSortedDictionary(x => x.Key, x => GroupByPrefix(x.Value, skip + x.Key.Length + 1)));
  }
}
