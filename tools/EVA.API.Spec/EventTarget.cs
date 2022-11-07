using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class EventTarget
{
  [JsonPropertyName("target")]
  public string Target { get; set; }

  [JsonPropertyName("type")]
  public string Type { get; set; }

  [JsonPropertyName("dataType")]
  public string? DataType { get; set; }
}