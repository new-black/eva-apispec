using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class DataModelInformation
{
  [JsonConstructor]
  public DataModelInformation(string name)
  {
    Name = name;
  }

  [JsonPropertyName("name")] public string Name { get; }
  [JsonPropertyName("lenient")] public bool Lenient { get; set; }
  [JsonPropertyName("supportsCustomID")] public bool SupportsBackendID { get; set; }
  [JsonPropertyName("supportsSystemID")] public bool SupportsSystemID { get; set; }
  [JsonPropertyName("isEvaID")] public bool IsEvaID { get; set; }
  [JsonPropertyName("isExternalID")] public bool IsExternalID { get; set; }
}