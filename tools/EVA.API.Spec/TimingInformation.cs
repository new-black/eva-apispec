using System.Text.Json.Serialization;

namespace EVA.Core.Typings.V2;

public class TimingInformation
{
  [JsonPropertyName("announced")] public long? Introduced { get; set; }
  [JsonPropertyName("active")] public long? Effective { get; set; }
  [JsonPropertyName("comment")] public string? Comment { get; set; }
}