using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class EventTarget
{
  [JsonPropertyName("target")]
  public string Target { get; set; }

  [JsonPropertyName("types")]
  public ImmutableArray<string> Types { get; set; }
}