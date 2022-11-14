using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class ErrorSpecification
{
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("message")]
  public string Message { get; set; }

  [JsonPropertyName("module")]
  public string Assembly { get; set; }

  [JsonPropertyName("arguments")]
  public ImmutableArray<ErrorSpecificationArgument> Arguments { get; set; }
}