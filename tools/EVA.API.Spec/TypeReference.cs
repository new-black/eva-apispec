using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.API.Spec;

public class TypeReference
{
  /// <summary>
  /// For generics, this is the un-constructed type
  /// </summary>
  [JsonPropertyName("ref")]
  public string Name { get; set; }

  /// <summary>
  /// Only for generic types, this is non-null
  /// </summary>
  [JsonPropertyName("args")]
  public ImmutableArray<TypeReference> Arguments { get; set; }

  /// <summary>
  /// Only for ref "option", this represents the shared interface of all classes
  /// </summary>
  [JsonPropertyName("shared")]
  public TypeReference? Shared { get; set; }

  /// <summary>
  /// Whether or not it can contain null
  /// </summary>
  [JsonPropertyName("nullable")]
  public bool Nullable { get; set; }

  public TypeReference(string name, ImmutableArray<TypeReference> arguments, bool nullable)
  {
    Name = name;
    Arguments = arguments;
    Nullable = nullable;
  }
}