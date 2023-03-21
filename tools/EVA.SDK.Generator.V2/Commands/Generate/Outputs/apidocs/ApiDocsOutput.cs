using System.Text.Json;
using System.Text.Json.Serialization;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

internal class ApiDocsOutput : IOutput<ApiDocsOptions>
{
  public string? OutputPattern => null;
  public string[] ForcedRemoves => new string[0];

  public async Task Write(OutputContext<ApiDocsOptions> ctx)
  {
    // List of all services
    await GenerateSidebar(ctx);

    // Generate each service
    foreach (var service in ctx.Input.Services)
    {
      await GenerateService(ctx, service);
    }
  }

  private static async Task GenerateService(OutputContext<ApiDocsOptions> ctx, ServiceModel service)
  {
    var model = new ServiceItem
    {
      Name = service.Name,
      Request = new ServiceItem.RequestItem
      {
        Properties = new List<ServiceItem.PropertyItem>()
      },
      Response = new ServiceItem.ResponseItem
      {
        Properties = new List<ServiceItem.PropertyItem>()
      }
    };

    // Request type
    var requestType = ctx.Input.Types[service.RequestTypeID];
    model.Description = requestType.Description;

    foreach (var (propName, propValue) in requestType.Properties)
    {
      model.Request.Properties.Add(new ServiceItem.PropertyItem
      {
        Name = propName,
        Description = propValue.Description,
        Type = ToTypeName(propValue.Type)
      });
    }

    // Response type
    var responseType = ctx.Input.Types[service.ResponseTypeID];
    foreach (var (propName, propValue) in responseType.Properties)
    {
      model.Response.Properties.Add(new ServiceItem.PropertyItem
      {
        Name = propName,
        Description = propValue.Description,
        Type = ToTypeName(propValue.Type)
      });
    }

    await ctx.Writer.WriteFileAsync($"services/{model.Name}.json", JsonSerializer.Serialize(model, JsonContext.Indented.ServiceItem));
  }

  private static string ToTypeName(TypeReference spec)
  {
    return spec.Name;
  }

  private static async Task GenerateSidebar(OutputContext<ApiDocsOptions> ctx)
  {
    var sidebar = new List<SidebarItem>();

    foreach (var service in ctx.Input.Services)
    {
      sidebar.Add(new SidebarItem
      {
        Type = "doc",
        Label = service.Name,
        ClassName = "api-method post",
        ID = $"api-reference/{service.Name}",
        Link = $"api-reference/{service.Name}"
      });
    }

    await ctx.Writer.WriteFileAsync("sidebar.json", JsonSerializer.Serialize(sidebar.ToArray(), JsonContext.Default.SidebarItemArray));
  }
}

internal class SidebarItem
{
  [JsonPropertyName("type")] public string Type { get; set; }
  [JsonPropertyName("id")] public string ID { get; set; }
  [JsonPropertyName("link")] public string Link { get; set; }
  [JsonPropertyName("label")] public string Label { get; set; }
  [JsonPropertyName("className")] public string ClassName { get; set; }
}

internal class ServiceItem
{
  [JsonPropertyName("name")] public string Name { get; set; }
  [JsonPropertyName("description")] public string? Description { get; set; }
  [JsonPropertyName("request")] public RequestItem Request { get; set; }
  [JsonPropertyName("response")] public ResponseItem Response { get; set; }

  public class RequestItem
  {
    [JsonPropertyName("properties")] public List<PropertyItem> Properties { get; set; }
  }

  public class ResponseItem
  {
    [JsonPropertyName("properties")] public List<PropertyItem> Properties { get; set; }
  }

  public class PropertyItem
  {
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
  }
}