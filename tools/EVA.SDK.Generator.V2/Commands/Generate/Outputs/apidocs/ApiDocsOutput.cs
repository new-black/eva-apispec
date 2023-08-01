using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

internal class ApiDocsOutput : IOutput<ApiDocsOptions>
{
  public string? OutputPattern => null;
  public string[] ForcedTransformations => new[] { RemoveGenerics.ID, RemoveOptions.ID, RemoveInheritance.ID, RemoveDataLakeExports.ID };

  public async Task Write(OutputContext<ApiDocsOptions> ctx)
  {
    // List of all services
    await GenerateSidebar(ctx);

    // Generate each service
    foreach (var service in ctx.Input.Services)
    {
      await GenerateService(ctx, service);
    }

    // Generate typesense output
    await GenerateTypesense(ctx);
  }

  private static async Task GenerateTypesense(OutputContext<ApiDocsOptions> ctx)
  {
    var output = new StringBuilder();

    foreach (var service in ctx.Input.Services)
    {
      var requestType = ctx.Input.Types[service.RequestTypeID];
      var id = service.Name.ToLowerInvariant();

      var o = new RootObject
      {
        Id = $"apidocs_{id}",
        Anchor = id,
        Content = requestType.Description,
        ContentCamel = requestType.Description,
        DocusaurusTag = "docs-default-current",
        Hierarchy = new Hierarchy { Lvl0 = "Developers", Lvl1 = service.Name },
        HierarchyLvl0 = "Developers",
        HierarchyLvl1 = service.Name,
        HierarchyCamel = new[] { new HierarchyCamel { Lvl0 = "Developers", Lvl1 = service.Name } },
        HierarchyRadio = new HierarchyRadio(),
        HierarchyRadioCamel = new HierarchyRadioCamel(),
        ItemPriority = 75,
        Language = "en",
        NoVariables = true,
        Tags = new[] { "login", "nb", "dev", "api" },
        Type = "content",
        Url = $"https://docs.newblack.io/documentation/api-reference/{service.Name}",
        UrlWithoutAnchor = $"https://docs.newblack.io/documentation/api-reference/{service.Name}",
        UrlWithoutVariables = $"https://docs.newblack.io/documentation/api-reference/{service.Name}",
        Version = new[] { "current" },
        Weight = new Weight { Level = 0, PageRank = 0, Position = 75 }
      };

      output.AppendLine(JsonSerializer.Serialize(o, JsonContext.Default.RootObject));
    }

    await ctx.Writer.WriteFileAsync("typesense.ndjson", output.ToString());
  }

  private static async Task GenerateService(OutputContext<ApiDocsOptions> ctx, ServiceModel service)
  {
    var model = new ServiceItem
    {
      Name = service.Name,
      Method = "POST",
      Path = service.Path,
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

    model.Request.Properties = FillRecursiveProperties<ServiceItem.RequestPropertyItem>(ctx.Input, new TypeReference(service.RequestTypeID, ImmutableArray<TypeReference>.Empty, false), x => new ServiceItem.RequestPropertyItem
    {
      Name = x.name,
      Description = x.property.Description,
      Type = ToTypeName(x.property.Type),
      Properties = x.nestedProperties,
      Recursion = x.nestedProperties is { Count: 0 },
      EnumValues = x.enumValues
    })!;

    // Response type
    model.Response.Properties = FillRecursiveProperties<ServiceItem.ResponsePropertyItem>(ctx.Input, new TypeReference(service.ResponseTypeID, ImmutableArray<TypeReference>.Empty, false), x => new ServiceItem.ResponsePropertyItem
    {
      Name = x.name,
      Description = x.property.Description,
      Type = ToTypeName(x.property.Type),
      Properties = x.nestedProperties,
      Recursion = x.nestedProperties is { Count: 0 },
      EnumValues = x.enumValues
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
    var sidebar = ctx.Input.Services.Select(service => new SidebarItem
    {
      Type = "doc",
      Label = service.Name,
      ClassName = "api-method post",
      ID = $"api-reference/{service.Name}",
      Link = $"api-reference/{service.Name}"
    }).ToList();

    await ctx.Writer.WriteFileAsync("sidebar.json", JsonSerializer.Serialize(sidebar.ToArray(), JsonContext.Default.SidebarItemArray));
  }

  private static Dictionary<string, long>? GetEnumValues(ApiDefinitionModel input, TypeReference? typeReference)
  {
    if (typeReference == null) return null;

    var propTypeName = typeReference.Name;

    // Arrays, we just recurse
    if (propTypeName == ApiSpecConsts.Specials.Array)
    {
      return GetEnumValues(input, typeReference.Arguments[0]);
    }

    // Option, we only expose the shared properties
    if (propTypeName == ApiSpecConsts.Specials.Option)
    {
      return GetEnumValues(input, typeReference.Shared);
    }

    // Primitives don't have properties
    if (ApiSpecConsts.AllPrimitives.Contains(propTypeName))
    {
      return null;
    }

    // TODO: Maps don't have properties (for now)
    if (propTypeName == ApiSpecConsts.Specials.Map)
    {
      return null;
    }

    var type = input.Types[propTypeName];
    if (type.EnumIsFlag == null)
    {
      return null;
    }

    return type.EnumValues.ToTotals();
  }

  private static List<TProperty>? FillRecursiveProperties<TProperty>(
    ApiDefinitionModel input,
    TypeReference? typeReference,
    Func<(string name, PropertySpecification property, List<TProperty>? nestedProperties, Dictionary<string, long>? enumValues), TProperty> propertyBuilder,
    Stack<string>? recursionGuard = null)
  {
    if (typeReference == null) return null;

    var propTypeName = typeReference.Name;

    // Arrays, we just recurse
    if (propTypeName == ApiSpecConsts.Specials.Array)
    {
      return FillRecursiveProperties(input, typeReference.Arguments[0], propertyBuilder, recursionGuard);
    }

    // Option, we only expose the shared properties
    if (propTypeName == ApiSpecConsts.Specials.Option)
    {
      return FillRecursiveProperties(input, typeReference.Shared, propertyBuilder, recursionGuard);
    }

    // Primitives don't have properties
    if (ApiSpecConsts.AllPrimitives.Contains(propTypeName))
    {
      return null;
    }

    // TODO: Maps don't have properties (for now)
    if (propTypeName == ApiSpecConsts.Specials.Map)
    {
      return null;
    }

    var type = input.Types[propTypeName];
    var properties = new List<TProperty>();

    recursionGuard ??= new Stack<string>();
    if (recursionGuard.Contains(propTypeName)) return properties;
    recursionGuard.Push(propTypeName);

    foreach (var (propName, propValue) in type.Properties)
    {
      // Figure out the nested properties for the type
      var nestedProperties = FillRecursiveProperties(input, propValue.Type, propertyBuilder, recursionGuard);

      // Figure out the enum values for the type
      var enumValues = GetEnumValues(input, propValue.Type);

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
  [JsonPropertyName("method")] public string Method { get; set; }
  [JsonPropertyName("path")] public string Path { get; set; }
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