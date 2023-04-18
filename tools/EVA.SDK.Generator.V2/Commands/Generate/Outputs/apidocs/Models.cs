using System.Text.Json.Serialization;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

public class RootObject
{
    public string anchor { get; set; }
    public string content { get; set; }
    public string content_camel { get; set; }
    public string docusaurus_tag { get; set; }
    public Hierarchy hierarchy { get; set; }
    [JsonPropertyName("hierarchy.lvl0")]
    public string hierarchy_lvl0 { get; set; }
    [JsonPropertyName("hierarchy.lvl1")]
    public string hierarchy_lvl1 { get; set; }
    public string hierarchy_lvl2 { get; set; }
    public Hierarchy_camel[] hierarchy_camel { get; set; }
    public Hierarchy_radio hierarchy_radio { get; set; }
    public Hierarchy_radio_camel hierarchy_radio_camel { get; set; }
    public int item_priority { get; set; }
    public string language { get; set; }
    public bool no_variables { get; set; }
    public string[] tags { get; set; }
    public string type { get; set; }
    public string url { get; set; }
    public string url_without_anchor { get; set; }
    public string url_without_variables { get; set; }
    public string[] version { get; set; }
    public Weight weight { get; set; }
}

public class Hierarchy
{
    public string lvl0 { get; set; }
    public string lvl1 { get; set; }
    public string lvl2 { get; set; }
    public object lvl3 { get; set; }
    public object lvl4 { get; set; }
    public object lvl5 { get; set; }
    public object lvl6 { get; set; }
}

public class Hierarchy_camel
{
    public string lvl0 { get; set; }
    public string lvl1 { get; set; }
    public string lvl2 { get; set; }
    public object lvl3 { get; set; }
    public object lvl4 { get; set; }
    public object lvl5 { get; set; }
    public object lvl6 { get; set; }
}

public class Hierarchy_radio
{
    public object lvl0 { get; set; }
    public object lvl1 { get; set; }
    public object lvl2 { get; set; }
    public object lvl3 { get; set; }
    public object lvl4 { get; set; }
    public object lvl5 { get; set; }
    public object lvl6 { get; set; }
}

public class Hierarchy_radio_camel
{
    public object lvl0 { get; set; }
    public object lvl1 { get; set; }
    public object lvl2 { get; set; }
    public object lvl3 { get; set; }
    public object lvl4 { get; set; }
    public object lvl5 { get; set; }
    public object lvl6 { get; set; }
}

public class Weight
{
    public int level { get; set; }
    public int page_rank { get; set; }
    public int position { get; set; }
}

