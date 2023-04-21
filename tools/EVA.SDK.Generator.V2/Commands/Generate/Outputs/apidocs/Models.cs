using System.Text.Json.Serialization;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

public class RootObject
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("anchor")] public string Anchor { get; set; }
    [JsonPropertyName("content")] public string Content { get; set; }
    [JsonPropertyName("content_camel")] public string ContentCamel { get; set; }
    [JsonPropertyName("docusaurus_tag")] public string DocusaurusTag { get; set; }
    [JsonPropertyName("hierarchy")] public Hierarchy Hierarchy { get; set; }
    [JsonPropertyName("hierarchy.lvl0")] public string HierarchyLvl0 { get; set; }
    [JsonPropertyName("hierarchy.lvl1")] public string HierarchyLvl1 { get; set; }
    [JsonPropertyName("hierarchy_camel")] public HierarchyCamel[] HierarchyCamel { get; set; }
    [JsonPropertyName("hierarchy_radio")] public HierarchyRadio HierarchyRadio { get; set; }
    [JsonPropertyName("hierarchy_radio_camel")] public HierarchyRadioCamel HierarchyRadioCamel { get; set; }
    [JsonPropertyName("item_priority")] public int ItemPriority { get; set; }
    [JsonPropertyName("language")] public string Language { get; set; }
    [JsonPropertyName("no_variables")] public bool NoVariables { get; set; }
    [JsonPropertyName("tags")] public string[] Tags { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; }
    [JsonPropertyName("url_without_anchor")] public string UrlWithoutAnchor { get; set; }
    [JsonPropertyName("url_without_variables")] public string UrlWithoutVariables { get; set; }
    [JsonPropertyName("version")] public string[] Version { get; set; }
    [JsonPropertyName("weight")] public Weight Weight { get; set; }
}

public class Hierarchy
{
    [JsonPropertyName("lvl0")] public string Lvl0 { get; set; }
    [JsonPropertyName("lvl1")] public string Lvl1 { get; set; }
    [JsonPropertyName("lvl2")] public string Lvl2 { get; set; }
    [JsonPropertyName("lvl3")] public object Lvl3 { get; set; }
    [JsonPropertyName("lvl4")] public object Lvl4 { get; set; }
    [JsonPropertyName("lvl5")] public object Lvl5 { get; set; }
    [JsonPropertyName("lvl6")] public object Lvl6 { get; set; }
}

public class HierarchyCamel
{
    [JsonPropertyName("lvl0")] public string Lvl0 { get; set; }
    [JsonPropertyName("lvl1")] public string Lvl1 { get; set; }
    [JsonPropertyName("lvl2")] public string? Lvl2 { get; set; }
    [JsonPropertyName("lvl3")] public object? Lvl3 { get; set; }
    [JsonPropertyName("lvl4")] public object? Lvl4 { get; set; }
    [JsonPropertyName("lvl5")] public object? Lvl5 { get; set; }
    [JsonPropertyName("lvl6")] public object? Lvl6 { get; set; }
}

public class HierarchyRadio
{
    [JsonPropertyName("lvl0")] public object Lvl0 { get; set; }
    [JsonPropertyName("lvl1")] public object Lvl1 { get; set; }
    [JsonPropertyName("lvl2")] public object Lvl2 { get; set; }
    [JsonPropertyName("lvl3")] public object Lvl3 { get; set; }
    [JsonPropertyName("lvl4")] public object Lvl4 { get; set; }
    [JsonPropertyName("lvl5")] public object Lvl5 { get; set; }
    [JsonPropertyName("lvl6")] public object Lvl6 { get; set; }
}

public class HierarchyRadioCamel
{
    [JsonPropertyName("lvl0")] public object Lvl0 { get; set; }
    [JsonPropertyName("lvl1")] public object Lvl1 { get; set; }
    [JsonPropertyName("lvl2")] public object Lvl2 { get; set; }
    [JsonPropertyName("lvl3")] public object Lvl3 { get; set; }
    [JsonPropertyName("lvl4")] public object Lvl4 { get; set; }
    [JsonPropertyName("lvl5")] public object Lvl5 { get; set; }
    [JsonPropertyName("lvl6")] public object Lvl6 { get; set; }
}

public class Weight
{
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("page_rank")] public int PageRank { get; set; }
    [JsonPropertyName("position")] public int Position { get; set; }
}

