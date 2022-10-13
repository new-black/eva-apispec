using System.Text.Json.Serialization;

namespace EVA.Core.Typings.V2;

public class TimedValue<T> : TimingInformation
{
  [JsonPropertyName("current")] public T CurrentValue { get; set; }
  [JsonPropertyName("new")] public T NewValue { get; set; }
}