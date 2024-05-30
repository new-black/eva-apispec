using System.Text.Json.Serialization;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

public class SingleService
{
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("method")]
  public string Method { get; set; }

  [JsonPropertyName("path")]
  public string Path { get; set; }

  [JsonPropertyName("description")]
  public string Description { get; set; }

  [JsonPropertyName("auth_description")]
  public string? AuthDescription { get; set; }

  [JsonPropertyName("headers")]
  public List<Header> Headers { get; set; }

  [JsonPropertyName("request_samples")]
  public List<RequestSample> RequestSamples { get; set; }

  [JsonPropertyName("response_samples")]
  public List<ResponseSample> ResponseSamples { get; set; }

  [JsonPropertyName("deprecation_notice")]
  public string? Deprecation { get; set; }

  [JsonPropertyName("request_type_id")]
  public string RequestTypeID { get; set; }

  [JsonPropertyName("response_type_id")]
  public string ResponseTypeID { get; set; }

  [JsonPropertyName("types")]
  public Dictionary<string, List<TypeInfo>> Types { get; set; }
}


public class Header
{
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("type")]
  public string Type { get; set; }

  [JsonPropertyName("description")]
  public string Description { get; set; }

  [JsonPropertyName("required")]
  public bool Required { get; set; }

  [JsonPropertyName("default")]
  public string? DefaultValue { get; set; }
}

public class RequestSample
{
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("sample")]
  public string Sample { get; set; }

  [JsonPropertyName("syntax")]
  public string Syntax { get; set; }
}

public class ResponseSample
{
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("sample")]
  public string Sample { get; set; }
}

public class TypeInfo
{
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("deprecation_notice")]
  public string? Deprecation { get; set; }

  [JsonPropertyName("description")]
  public string Description { get; set; }

  [JsonPropertyName("type")]
  public string Type { get; set; }

  [JsonPropertyName("required")]
  public bool Required { get; set; }

  [JsonPropertyName("properties_id")]
  public string? PropertiesID { get; set; }

  [JsonPropertyName("one_of")]
  public OneOf[]? OneOf { get; set; }
}

public class OneOf
{
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("properties_id")]
  public string? PropertiesID { get; set; }
}