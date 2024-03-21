using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class StringLengthConstraint
{
  [JsonPropertyName("min")] public int Min { get; set; }
  [JsonPropertyName("max")] public int Max { get; set; }
}

public class StringRegexConstraint
{
  [JsonPropertyName("regex")] public string Regex { get; set; }
}