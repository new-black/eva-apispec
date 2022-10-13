using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.Core.Typings.V2;

public class TypeSpecification
{
  [JsonIgnore] internal string ID { get; set; }
  [JsonPropertyName("typename")] public string TypeName { get; set; }
  [JsonPropertyName("assembly")] public string Assembly { get; set; }
  [JsonPropertyName("description")] public string? Description { get; set; }
  [JsonIgnore] internal TypeContext Context { get; set; }
  [JsonPropertyName("usage")] public TypeUsage Usage { get; set; }
  [JsonPropertyName("enumIsFlag")] public bool? EnumIsFlag { get; set; }
  [JsonPropertyName("typeArguments")] public ImmutableArray<string> TypeArguments { get; set; } = ImmutableArray<string>.Empty;
  [JsonPropertyName("typeDependencies")] public ImmutableArray<string> TypeDependencies { get; set; } = ImmutableArray<string>.Empty;
  [JsonPropertyName("extends")] public TypeReference? Extends { get; set; }
  [JsonPropertyName("properties")] public ImmutableSortedDictionary<string, PropertySpecification> Properties { get; set; }
  [JsonPropertyName("enumValues")] public ImmutableSortedDictionary<string, EnumValueSpecification> EnumValues { get; set; }
  [JsonPropertyName("parent")] public string? ParentType { get; set; }
}