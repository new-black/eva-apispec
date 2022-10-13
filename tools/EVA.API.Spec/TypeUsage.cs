using System.Text.Json.Serialization;

namespace EVA.Core.Typings.V2;

public class TypeUsage
{
  [JsonPropertyName("request")] public bool Request { get; set; }
  [JsonPropertyName("response")] public bool Response { get; set; }
}