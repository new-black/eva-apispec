using System.Text.Json.Serialization;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

public class RootObject
{
    [JsonPropertyName("id")] public string id { get; set; }
    [JsonPropertyName("anchor")] public string anchor { get; set; }
    [JsonPropertyName("content")] public string content { get; set; }
    [JsonPropertyName("content_camel")] public string content_camel { get; set; }
    [JsonPropertyName("docusaurus_tag")] public string docusaurus_tag { get; set; }
    [JsonPropertyName("hierarchy")] public Hierarchy hierarchy { get; set; }
    [JsonPropertyName("hierarchy.lvl0")] public string hierarchy_lvl0 { get; set; }
    [JsonPropertyName("hierarchy.lvl1")] public string hierarchy_lvl1 { get; set; }
    [JsonPropertyName("hierarchy_camel")] public Hierarchy_camel[] hierarchy_camel { get; set; }
    [JsonPropertyName("hierarchy_radio")] public Hierarchy_radio hierarchy_radio { get; set; }
    [JsonPropertyName("hierarchy_radio_camel")] public Hierarchy_radio_camel hierarchy_radio_camel { get; set; }
    [JsonPropertyName("item_priority")] public int item_priority { get; set; }
    [JsonPropertyName("language")] public string language { get; set; }
    [JsonPropertyName("no_variables")] public bool no_variables { get; set; }
    [JsonPropertyName("tags")] public string[] tags { get; set; }
    [JsonPropertyName("type")] public string type { get; set; }
    [JsonPropertyName("url")] public string url { get; set; }
    [JsonPropertyName("url_without_anchor")] public string url_without_anchor { get; set; }
    [JsonPropertyName("url_without_variables")] public string url_without_variables { get; set; }
    [JsonPropertyName("version")] public string[] version { get; set; }
    [JsonPropertyName("weight")] public Weight weight { get; set; }
}

public class Hierarchy
{
    [JsonPropertyName("lvl0")] public string lvl0 { get; set; }
    [JsonPropertyName("lvl1")] public string lvl1 { get; set; }
    [JsonPropertyName("lvl2")] public string lvl2 { get; set; }
    [JsonPropertyName("lvl3")] public object lvl3 { get; set; }
    [JsonPropertyName("lvl4")] public object lvl4 { get; set; }
    [JsonPropertyName("lvl5")] public object lvl5 { get; set; }
    [JsonPropertyName("lvl6")] public object lvl6 { get; set; }
}

public class Hierarchy_camel
{
    [JsonPropertyName("lvl0")] public string lvl0 { get; set; }
    [JsonPropertyName("lvl1")] public string lvl1 { get; set; }
    [JsonPropertyName("lvl2")] public string lvl2 { get; set; }
    [JsonPropertyName("lvl3")] public object lvl3 { get; set; }
    [JsonPropertyName("lvl4")] public object lvl4 { get; set; }
    [JsonPropertyName("lvl5")] public object lvl5 { get; set; }
    [JsonPropertyName("lvl6")] public object lvl6 { get; set; }
}

public class Hierarchy_radio
{
    [JsonPropertyName("lvl0")] public object lvl0 { get; set; }
    [JsonPropertyName("lvl1")] public object lvl1 { get; set; }
    [JsonPropertyName("lvl2")] public object lvl2 { get; set; }
    [JsonPropertyName("lvl3")] public object lvl3 { get; set; }
    [JsonPropertyName("lvl4")] public object lvl4 { get; set; }
    [JsonPropertyName("lvl5")] public object lvl5 { get; set; }
    [JsonPropertyName("lvl6")] public object lvl6 { get; set; }
}

public class Hierarchy_radio_camel
{
    [JsonPropertyName("lvl0")] public object lvl0 { get; set; }
    [JsonPropertyName("lvl1")] public object lvl1 { get; set; }
    [JsonPropertyName("lvl2")] public object lvl2 { get; set; }
    [JsonPropertyName("lvl3")] public object lvl3 { get; set; }
    [JsonPropertyName("lvl4")] public object lvl4 { get; set; }
    [JsonPropertyName("lvl5")] public object lvl5 { get; set; }
    [JsonPropertyName("lvl6")] public object lvl6 { get; set; }
}

public class Weight
{
    [JsonPropertyName("level")] public int level { get; set; }
    [JsonPropertyName("page_rank")] public int page_rank { get; set; }
    [JsonPropertyName("position")] public int position { get; set; }
}

