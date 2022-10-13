using System.Text.Json.Serialization;

namespace EVA.Core.Typings.V2;

public class PermissionInformation
{
  [JsonPropertyName("types")] public ApiSpecUserTypes? UserTypes { get; set; }
  [JsonPropertyName("functionality")] public string? Functionality { get; set; }
  [JsonPropertyName("scope")] public ApiSpecFuncScope? Scope { get; set; }
}