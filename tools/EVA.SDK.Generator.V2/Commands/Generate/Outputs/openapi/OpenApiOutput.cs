using System.Collections.Immutable;
using System.Text.RegularExpressions;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;

internal partial class OpenApiOutput : IOutput<OpenApiOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedRemoves => new[] { "generics", "unused-type-params", "errors", "event-exports" };

  public async Task Write(OutputContext<OpenApiOptions> ctx)
  {
    var model = GetModel(ctx.Input, ctx.Options.Host);

    var version = ctx.Options.Version switch
    {
      "v2" => OpenApiSpecVersion.OpenApi2_0,
      _ => OpenApiSpecVersion.OpenApi3_0
    };

    var filename = ctx.Options.Format == "yaml" ? "openapi.yaml" : "openapi.json";
    await using var file = ctx.Writer.WriteStreamAsync(filename);
    await using var textWriter = new StreamWriter(file.Value);

    IOpenApiWriter openApiWriter = ctx.Options.Format == "yaml"
      ? new OpenApiYamlWriter(textWriter, new OpenApiWriterSettings())
      : new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings { Terse = ctx.Options.Terse });

    model.Serialize(openApiWriter, version);
  }

  internal static OpenApiDocument GetModel(ApiDefinitionModel input, string host)
  {
    // Base
    var model = new OpenApiDocument
    {
      Info = new OpenApiInfo
      {
        Version = "1.0.0",
        Description = "OpenApi description of EVA",
        Title = "EVA",
        Contact = new OpenApiContact
        {
          Email = "ruben.oost@newblack.io",
          Name = "Ruben Oost",
          Url = new Uri("https://newblack.io/")
        }
      },
      Servers = new List<OpenApiServer>
      {
        new() { Url = host }
      },
      Paths = new OpenApiPaths(),
      Components = new OpenApiComponents(),
      SecurityRequirements = new List<OpenApiSecurityRequirement>
      {
        new()
        {
          { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "eva-auth" } }, new List<string>() }
        }
      }
    };

    model.Components.SecuritySchemes.Add("eva-auth", new OpenApiSecurityScheme
    {
      Description = "The default authentication mechanism when communicating with EVA",
      Type = SecuritySchemeType.ApiKey,
      Name = "Authorization",
      In = ParameterLocation.Header
    });

    // Render each service
    foreach (var service in input.Services)
    {
      model.Paths[service.Path] = ToPathItem(input, service);
    }

    // Render each type
    foreach (var (id, type) in input.Types)
    {
      model.Components.Schemas.Add(FixName(id), ToSchema(input, type));
    }

    return model;
  }

  private static OpenApiSchema ToSchema(ApiDefinitionModel input, TypeSpecification type)
  {
    if (type.EnumIsFlag.HasValue)
    {
      var totals = type.EnumValues.ToTotals();
      var possibleValues = string.Join('\n', totals.Select(kv => $"* `{kv.Value}` - {kv.Key}"));

      if (type.EnumIsFlag is true)
      {
        return new OpenApiSchema
        {
          Type = "integer",
          Description = $"Flags enum, combine any of the below values:\n\n{possibleValues}"
        };
      }

      if (type.EnumIsFlag is false)
      {
        return new OpenApiSchema
        {
          Type = "integer",
          Enum = totals.Select(kv => new OpenApiInteger((int) kv.Value) as IOpenApiAny).ToList(),
          Description = $"Possible values:\n\n{possibleValues}"
        };
      }
    }

    var result = new OpenApiSchema
    {
      Type = "object",
      AdditionalPropertiesAllowed = false,
      Properties = new Dictionary<string, OpenApiSchema>()
    };

    foreach (var (name, prop) in type.Properties)
    {
      var schema = ToSchema(input, prop.Type);
      schema.Description = prop.Description;
      result.Properties.Add(name, schema);
    }

    result.Required = type.Properties.Where(p => !p.Value.Type.Nullable).Select(p => p.Key).OrderBy(x => x).ToImmutableHashSet();

    if (type.Extends != null)
    {
      result.AllOf = new List<OpenApiSchema> { ToSchema(input, type.Extends) };
    }

    return result;
  }

  private static OpenApiSchema ToSchema(ApiDefinitionModel input, TypeReference type)
  {
    if (type.Name == ApiSpecConsts.Int16) return new OpenApiSchema { Type = "integer" };
    if (type.Name == ApiSpecConsts.Int32) return new OpenApiSchema { Type = "integer" };
    if (type.Name == ApiSpecConsts.Int64) return new OpenApiSchema { Type = "integer" };
    if (type.Name == ApiSpecConsts.String) return new OpenApiSchema { Type = "string" };
    if (type.Name == ApiSpecConsts.Binary) return new OpenApiSchema { Type = "string" };
    if (type.Name == ApiSpecConsts.Bool) return new OpenApiSchema { Type = "boolean" };
    if (type.Name == ApiSpecConsts.Guid) return new OpenApiSchema { Type = "string", Format = "uuid" };
    if (type.Name == ApiSpecConsts.Float32) return new OpenApiSchema { Type = "number" };
    if (type.Name == ApiSpecConsts.Float64) return new OpenApiSchema { Type = "number" };
    if (type.Name == ApiSpecConsts.Float128) return new OpenApiSchema { Type = "number" };
    if (type.Name == ApiSpecConsts.Duration) return new OpenApiSchema { Type = "string" };
    if (type.Name == ApiSpecConsts.Object) return new OpenApiSchema { Type = "object", AdditionalPropertiesAllowed = true };
    if (type.Name == ApiSpecConsts.Any) return new OpenApiSchema { Type = "object", AdditionalPropertiesAllowed = true };
    if (type.Name == ApiSpecConsts.Date) return new OpenApiSchema { Type = "string", Format = "date-time" };
    if (type.Name == ApiSpecConsts.Specials.Array) return new OpenApiSchema { Type = "array", Items = ToSchema(input, type.Arguments.Single()) };
    if (type.Name == ApiSpecConsts.Specials.Option) return new OpenApiSchema { AnyOf = type.Arguments.Select(a => ToSchema(input, a)).ToList() };

    if (type.Name == ApiSpecConsts.Specials.Map)
    {
      var keyType = type.Arguments[0].Name;
      if (keyType is ApiSpecConsts.String or ApiSpecConsts.Int64 or ApiSpecConsts.Float128 or ApiSpecConsts.Date || char.IsUpper(keyType[0]) && input.Types[keyType].EnumIsFlag.HasValue)
      {
        return new OpenApiSchema
        {
          Type = "object",
          AdditionalPropertiesAllowed = true,
          AdditionalProperties = ToSchema(input, type.Arguments[1])
        };
      }
    }

    if (!type.Name.StartsWith("_") && !type.Arguments.Any())
    {
      return ToSchema(type.Name);
    }

    throw new SdkException($"Cannot build openapi schema from {type.Name}");
  }

  private static OpenApiSchema ToSchema(string id)
  {
    return new OpenApiSchema
    {
      Reference = new OpenApiReference
      {
        Id = FixName(id),
        Type = ReferenceType.Schema
      }
    };
  }

  private static OpenApiPathItem ToPathItem(ApiDefinitionModel input, ServiceModel service)
  {
    return new OpenApiPathItem
    {
      Summary = service.Name,
      Description = input.Types[service.RequestTypeID].Description ?? $"The {service.Name} service",
      Parameters = new List<OpenApiParameter>
      {
        new()
        {
          In = ParameterLocation.Header,
          Name = "EVA-User-Agent",
          Required = true,
          AllowEmptyValue = false,
          Schema = new OpenApiSchema
          {
            Default = new OpenApiString("eva-sdk-openapi"),
            Type = "string"
          },
          Style = ParameterStyle.Simple
        }
      },
      Operations = new Dictionary<OperationType, OpenApiOperation>
      {
        {
          OperationType.Post, new OpenApiOperation
          {
            Summary = service.RequestTypeID,
            Description = input.Types[service.RequestTypeID].Description ?? $"The {service.Name} service",
            OperationId = service.Name,
            Tags = new List<OpenApiTag> { new() { Name = TagFromAssembly(service.Assembly), Description = TagFromAssembly(service.Assembly) } },
            Security = new List<OpenApiSecurityRequirement>
            {
              new()
              {
                {
                  new OpenApiSecurityScheme
                  {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "eva-auth" }
                  },
                  ImmutableList<string>.Empty
                }
              }
            },
            RequestBody = new OpenApiRequestBody
            {
              Description = "",
              Required = true,
              Content = new Dictionary<string, OpenApiMediaType>
              {
                {
                  "application/json", new OpenApiMediaType
                  {
                    Schema = ToSchema(service.RequestTypeID)
                  }
                }
              }
            },
            Responses = new OpenApiResponses
            {
              {
                "200", new OpenApiResponse
                {
                  Description = $"The response for a call to {service.Name}",
                  Content = new Dictionary<string, OpenApiMediaType>
                  {
                    {
                      "application/json", new OpenApiMediaType
                      {
                        Schema = ToSchema(service.ResponseTypeID)
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    };
  }

  private static string FixName(string name)
  {
    return NameRegex().Replace(name, "_").Trim('_');
  }

  private static string TagFromAssembly(string name)
  {
    return name.StartsWith("EVA.") ? name[4..] : name;
  }

  [GeneratedRegex("[^a-zA-Z0-9-._]", RegexOptions.Compiled)]
  private static partial Regex NameRegex();
}