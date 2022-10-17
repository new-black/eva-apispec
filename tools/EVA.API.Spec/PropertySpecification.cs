using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class PropertySpecification
{
  [JsonPropertyName("type")] public TypeReference Type { get; set; }
  [JsonPropertyName("description")] public string? Description { get; set; }
  [JsonPropertyName("skippable")] public bool Skippable { get; set; }
  [JsonPropertyName("deprecated")] public TimingInformation? Deprecated { get; set; }
  [JsonPropertyName("required")] public TimedValue<bool?> Required { get; set; }
  [JsonPropertyName("requiredAllowEmpty")] public TimedValue<bool?> RequiredAllowEmpty { get; set; }
  [JsonPropertyName("minValue")] public TimedValue<double?> MinValue { get; set; }
  [JsonPropertyName("maxValue")] public TimedValue<double?> MaxValue { get; set; }
  [JsonPropertyName("stringLengthConstraint")] public StringLengthConstraint StringLengthConstraint { get; set; }
  [JsonPropertyName("datamodel")] public DataModelInformation? DataModelInformation { get; set; }
  [JsonPropertyName("allowedValues")] public ImmutableArray<string> AllowedValues { get; set; } = ImmutableArray<string>.Empty;
}