using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class ErrorSpecification
{
  [JsonConstructor]
  public ErrorSpecification(string name, string message, string assembly)
  {
    Name = name;
    Message = message;
    Assembly = assembly;
  }

  [JsonPropertyName("name")]
  public string Name { get; }

  [JsonPropertyName("message")]
  public string Message { get; }

  [JsonPropertyName("module")]
  public string Assembly { get; set; }

  [JsonPropertyName("arguments")]
  public ImmutableArray<ErrorSpecificationArgument> Arguments { get; set; } = ImmutableArray<ErrorSpecificationArgument>.Empty;
}