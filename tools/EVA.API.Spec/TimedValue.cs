using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class TimedValue<T> : TimingInformation
{
  [JsonConstructor]
  public TimedValue(T currentValue, T newValue)
  {
    CurrentValue = currentValue;
    NewValue = newValue;
  }

  [JsonPropertyName("current")] public T CurrentValue { get; }
  [JsonPropertyName("new")] public T NewValue { get; }
}