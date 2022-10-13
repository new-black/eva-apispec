using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.Core.Typings.V2;

public class ApiDefinitionModel
{
  [JsonPropertyName("services")] public ImmutableArray<ServiceModel> Services { get; set; }

  [JsonPropertyName("types")] public ImmutableSortedDictionary<string, TypeSpecification> Types { get; set; }
}