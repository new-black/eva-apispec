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
      Name = service.Name
    };

    var requestType = ctx.Input.Types[service.RequestTypeID];
    var responseType = ctx.Input.Types[service.ResponseTypeID];

    model.Description = requestType.Description;

    await ctx.Writer.WriteFileAsync($"services/{model.Name}.json", JsonSerializer.Serialize(model, JsonContext.Indented.ServiceItem));
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
        ID = $"api-reference/{service.Name}"
      });
    }

    await ctx.Writer.WriteFileAsync("sidebar.json", JsonSerializer.Serialize(sidebar.ToArray(), JsonContext.Default.SidebarItemArray));
  }
}

internal class SidebarItem
{
  [JsonPropertyName("type")] public string Type { get; set; }
  [JsonPropertyName("id")] public string ID { get; set; }
  [JsonPropertyName("label")] public string Label { get; set; }
  [JsonPropertyName("className")] public string ClassName { get; set; }
}

internal class ServiceItem
{
  [JsonPropertyName("name")] public string Name { get; set; }
  [JsonPropertyName("description")] public string? Description { get; set; }
}