using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class TypeSpecification
{
  public TypeSpecification(string typeName, string assembly, TypeUsage usage)
  {
    TypeName = typeName;
    Assembly = assembly;
    Usage = usage;
  }

  [JsonPropertyName("typename")] public string TypeName { get; set; }
  [JsonPropertyName("assembly")] public string Assembly { get; set; }
  [JsonPropertyName("description")] public string? Description { get; set; }
  [JsonPropertyName("usage")] public TypeUsage Usage { get; set; }
  [JsonPropertyName("enumIsFlag")] public bool? EnumIsFlag { get; set; }
  [JsonPropertyName("typeArguments")] public ImmutableArray<string> TypeArguments { get; set; } = ImmutableArray<string>.Empty;
  [JsonPropertyName("typeDependencies")] public ImmutableArray<string> TypeDependencies { get; set; } = ImmutableArray<string>.Empty;
  [JsonPropertyName("extends")] public TypeReference? Extends { get; set; }
  [JsonPropertyName("properties")] public ImmutableSortedDictionary<string, PropertySpecification> Properties { get; set; } = ImmutableSortedDictionary<string, PropertySpecification>.Empty;
  [JsonPropertyName("enumValues")] public ImmutableSortedDictionary<string, EnumValueSpecification> EnumValues { get; set; } = ImmutableSortedDictionary<string, EnumValueSpecification>.Empty;
  [JsonPropertyName("parent")] public string? ParentType { get; set; }
}