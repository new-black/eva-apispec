using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class ServiceModel
{
  [JsonConstructor]
  public ServiceModel(string name, string assembly, string path, string requestTypeID, string responseTypeID, AuthInformation authInformation)
  {
    Name = name;
    Assembly = assembly;
    Path = path;
    RequestTypeID = requestTypeID;
    ResponseTypeID = responseTypeID;
    AuthInformation = authInformation;
  }

  [JsonPropertyName("name")] public string Name { get; set; }
  [JsonPropertyName("assembly")] public string Assembly { get; set; }
  [JsonPropertyName("path")] public string Path { get; set; }
  [JsonPropertyName("requestType")] public string RequestTypeID { get; set; }
  [JsonPropertyName("responseType")] public string ResponseTypeID { get; set; }
  [JsonPropertyName("deprecated")] public TimingInformation? Deprecated { get; set; }
  [JsonPropertyName("auth")] public AuthInformation AuthInformation { get; set; }
}