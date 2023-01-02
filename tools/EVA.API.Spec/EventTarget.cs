using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class EventTarget
{
  [JsonConstructor]
  public EventTarget(string target, string type)
  {
    Target = target;
    Type = type;
  }

  [JsonPropertyName("target")]
  public string Target { get; }

  [JsonPropertyName("type")]
  public string Type { get; }

  [JsonPropertyName("dataType")]
  public string? DataType { get; set; }
}