using System.Text.Json.Serialization;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

public class SingleDataLake
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("type_id")]
    public string TypeID { get; set; }

    [JsonPropertyName("types")]
    public Dictionary<string, List<TypeInfo>> Types { get; set; }
}