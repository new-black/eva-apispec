using System.Text.Json.Serialization;

namespace EVA.Core.Typings.V2;

public class StringLengthConstraint
{
  [JsonPropertyName("min")] public int Min { get; set; }
  [JsonPropertyName("max")] public int Max { get; set; }
}