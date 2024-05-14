using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

public class ServiceIndex
{
  [JsonPropertyName("entries")]
  public ImmutableArray<Entry> Entries { get; set; } = ImmutableArray<Entry>.Empty;

  public class Entry
  {
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; }
  }
}