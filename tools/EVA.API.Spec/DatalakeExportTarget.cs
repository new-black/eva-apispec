using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class DatalakeExportTarget
{
  [JsonPropertyName("Name")] public string Name { get; set; }
  [JsonPropertyName("DataType")] public string DataType { get; set; }

  [JsonConstructor]
  public DatalakeExportTarget(string name, string dataType)
  {
    Name = name;
    DataType = dataType;
  }
}