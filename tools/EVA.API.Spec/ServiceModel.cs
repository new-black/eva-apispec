using System.Text.Json.Serialization;

namespace EVA.Core.Typings.V2;

public class ServiceModel
{
  [JsonPropertyName("name")] public string Name { get; set; }
  [JsonPropertyName("assembly")] public string Assembly { get; set; }
  [JsonPropertyName("path")] public string Path { get; set; }
  [JsonPropertyName("requestType")] public string RequestTypeID { get; set; }
  [JsonPropertyName("responseType")] public string ResponseTypeID { get; set; }
  [JsonPropertyName("deprecated")] public TimingInformation? Deprecated { get; set; }
  [JsonPropertyName("auth")] public AuthInformation AuthInformation { get; set; }
}