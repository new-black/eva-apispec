using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class DatalakeExportTarget
{
  [JsonPropertyName("Name")] public string Name { get; set; }
  [JsonPropertyName("DataType")] public string DataType { get; set; }
  [JsonPropertyName("ExamplePath")] public string? ExamplePath { get; set; }

  [JsonConstructor]
  public DatalakeExportTarget(string name, string dataType, string? examplePath)
  {
    Name = name;
    DataType = dataType;
    ExamplePath = examplePath;
  }
}