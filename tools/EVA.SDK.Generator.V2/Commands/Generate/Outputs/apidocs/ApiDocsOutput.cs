using System.Text.Json;
using System.Text.Json.Serialization;
using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

internal class ApiDocsOutput : IOutput<ApiDocsOptions>
{
  public string? OutputPattern => null;
  public string[] ForcedRemoves => new[] { "generics", "options" };

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
        Properties = new List<ServiceItem.RequestPropertyItem>()
      },
      Response = new ServiceItem.ResponseItem
      {
        Properties = new List<ServiceItem.ResponsePropertyItem>()
      }
    };

    // Request type
    var requestType = ctx.Input.Types[service.RequestTypeID];
    model.Description = requestType.Description;

    model.Request.Properties = FillRecursiveProperties<ServiceItem.RequestPropertyItem>(ctx.Input, service.RequestTypeID, x =>
    {
      return new ServiceItem.RequestPropertyItem
      {
        Name = x.name,
        Description = x.property.Description,
        Type = ToTypeName(x.property.Type),
        Properties = x.nestedProperties,
        Recursion = x.nestedProperties is { Count: 0 },
        EnumValues = x.enumValues
      };
    })!;

    // Response type
    model.Response.Properties = FillRecursiveProperties<ServiceItem.ResponsePropertyItem>(ctx.Input, service.ResponseTypeID, x =>
    {
      return new ServiceItem.ResponsePropertyItem()
      {
        Name = x.name,
        Description = x.property.Description,
        Type = ToTypeName(x.property.Type),
        Properties = x.nestedProperties,
        Recursion = x.nestedProperties is { Count: 0 },
        EnumValues = x.enumValues
      };
    })!;

    await ctx.Writer.WriteFileAsync($"services/{model.Name}.json", JsonSerializer.Serialize(model, JsonContext.Indented.ServiceItem));
  }

  private static string ToTypeName(TypeReference spec)
  {
    var n = spec.Nullable ? "?" : "";

    if (spec.Name == ApiSpecConsts.Specials.Array)
    {
      return $"{ToTypeName(spec.Arguments[0])}[]{n}";
    }

    if (spec.Name == ApiSpecConsts.Specials.Map)
    {
      return $"{{{ToTypeName(spec.Arguments[0])}: {ToTypeName(spec.Arguments[1])}}}{n}";
    }

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

  /// <summary>
  /// Empty list of properties means that it got stopped by the recursion guard.
  /// </summary>
  /// <param name="input"></param>
  /// <param name="id"></param>
  /// <param name="propertyBuilder"></param>
  /// <param name="recursionGuard"></param>
  /// <typeparam name="TProperty"></typeparam>
  /// <returns></returns>
  private static List<TProperty>? FillRecursiveProperties<TProperty>(
    ApiDefinitionModel input,
    string id,
    Func<(string name, PropertySpecification property, List<TProperty>? nestedProperties, Dictionary<string, long>? enumValues), TProperty> propertyBuilder,
    Stack<string>? recursionGuard = null)
  {
    var type = input.Types[id];
    var properties = new List<TProperty>();

    recursionGuard ??= new Stack<string>();
    if (recursionGuard.Contains(id)) return properties;
    recursionGuard.Push(id);

    foreach (var (propName, propValue) in type.Properties)
    {
      // Figure out the nested properties
      List<TProperty>? nestedProperties = null;
      Dictionary<string, long>? enumValues = null;

      if (!ApiSpecConsts.AllPrimitives.Contains(propValue.Type.Name)
          && propValue.Type.Name != ApiSpecConsts.Specials.Array
          && propValue.Type.Name != ApiSpecConsts.Specials.Map
          && propValue.Type.Name != ApiSpecConsts.Specials.Option)
      {
        // This is a regular type, so we could map the properties
        var propertyType = input.Types[propValue.Type.Name];
        if (propertyType.EnumIsFlag == null)
        {
          nestedProperties = FillRecursiveProperties(input, propValue.Type.Name, propertyBuilder);
        }
        else
        {
          long TotalValue(string name)
          {
            var value = propertyType.EnumValues[name];
            if (value == null) return 0;
            return value.Value + value.Extends.Sum(TotalValue);
          }

          enumValues = propertyType.EnumValues.ToDictionary(x => x.Key, x => TotalValue(x.Key));
        }
      }

      var property = propertyBuilder((propName, propValue, nestedProperties, enumValues));
      properties.Add(property);
    }

    recursionGuard.Pop();
    return properties.Count == 0 ? null : properties;
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
    [JsonPropertyName("properties")] public List<RequestPropertyItem> Properties { get; set; }
  }

  public class ResponseItem
  {
    [JsonPropertyName("properties")] public List<ResponsePropertyItem> Properties { get; set; }
  }

  public class RequestPropertyItem
  {
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("properties")] public List<RequestPropertyItem>? Properties { get; set; }
    [JsonPropertyName("recursion")] public bool Recursion { get; set; }
    [JsonPropertyName("enumValues")] public Dictionary<string, long>? EnumValues { get; set; }
  }

  public class ResponsePropertyItem
  {
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("properties")] public List<ResponsePropertyItem>? Properties { get; set; }
    [JsonPropertyName("recursion")] public bool Recursion { get; set; }
    [JsonPropertyName("enumValues")] public Dictionary<string, long>? EnumValues { get; set; }
  }
}