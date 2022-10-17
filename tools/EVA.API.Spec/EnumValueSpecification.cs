using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class EnumValueSpecification
{
  [JsonPropertyName("value")] public long Value { get; set; }
  [JsonPropertyName("extends")] public ImmutableArray<string> Extends { get; set; } = ImmutableArray<string>.Empty;
}