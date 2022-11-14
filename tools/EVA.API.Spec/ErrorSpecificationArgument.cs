using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class ErrorSpecificationArgument
{
  [JsonPropertyName("type")]
  public TypeReference Type { get; set; }

  [JsonPropertyName("name")]
  public string? Name { get; set; }
}