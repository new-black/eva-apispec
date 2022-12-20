using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class DataModelInformation
{
  [JsonPropertyName("name")] public string Name { get; set; }
  [JsonPropertyName("lenient")] public bool Lenient { get; set; }
  [JsonPropertyName("supportsCustomID")] public bool SupportsBackendID { get; set; }
  [JsonPropertyName("isEvaID")] public bool IsEvaID { get; set; }
  [JsonPropertyName("isExternalID")] public bool IsExternalID { get; set; }
}