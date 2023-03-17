using System.Text.Json;
using System.Text.Json.Serialization;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

internal class ApiDocsOutput : IOutput<ApiDocsOptions>
{
  public string? OutputPattern => null;
  public string[] ForcedRemoves => new string[0];

  public async Task Write(OutputContext<ApiDocsOptions> ctx)
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