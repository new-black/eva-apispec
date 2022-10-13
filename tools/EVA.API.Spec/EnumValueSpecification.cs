using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.Core.Typings.V2;

public class EnumValueSpecification
{
  [JsonPropertyName("value")] public long Value { get; set; }
  [JsonPropertyName("extends")] public ImmutableArray<string> Extends { get; set; } = ImmutableArray<string>.Empty;
}