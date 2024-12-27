using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class ErrorSpecificationArgument
{
  [JsonConstructor]
  public ErrorSpecificationArgument(TypeReference type, string name)
  {
    Type = type;
    Name = name;
  }

  [JsonPropertyName("type")]
  public TypeReference Type { get; }

  [JsonPropertyName("name")]
  public string? Name { get; }
}