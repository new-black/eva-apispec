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

  private const string Parameter_Header_UserAgent = "p1";
  private const string Parameter_Header_IdsMode = "p2";
  private const string Parameter_Header_AppToken = "p3";
  private const string Parameter_Header_ElevationToken = "p4";

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
      Tags = input.Services.Select(s => s.Assembly).Distinct().Select(s => new OpenApiTag { Name = s, Description = s }).ToList(),
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

    // Parameters
    model.Components.Parameters.Add(Parameter_Header_UserAgent, new()
    {
      In = ParameterLocation.Header,
      Name = "EVA-User-Agent",
      Description = "The user agent that is making these calls. Don't make this specific per device/browser but per application. This should be of the form: MyFirstUserAgent/1.0.0",
      Required = true,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Default = new OpenApiString("eva-sdk-openapi"),
        Type = "string"
      },
      Style = ParameterStyle.Simple
    });
    model.Components.Parameters.Add(Parameter_Header_IdsMode, new()
    {
      In = ParameterLocation.Header,
      Description = "The IDs mode to run this request in. Currently only `ExternalIDs` is supported.",
      Name = "EVA-IDs-Mode",
      Required = false,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Type = "string",
        Enum = new List<IOpenApiAny>
        {
          new OpenApiString("ExternalIDs")
        }
      },
      Style = ParameterStyle.Simple
    });
    model.Components.Parameters.Add(Parameter_Header_AppToken, new()
    {
      In = ParameterLocation.Header,
      Name = "EVA-App-Token",
      Description = "The app token, used for anonymously building an order",
      Required = false,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Type = "string"
      },
      Style = ParameterStyle.Simple
    });
    model.Components.Parameters.Add(Parameter_Header_ElevationToken, new()
    {
      In = ParameterLocation.Header,
      Name = "EVA-Elevation-Token",
      Description = "Token used for one-time elevation of a user's permissions",
      Required = false,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Type = "string"
      },
      Style = ParameterStyle.Simple
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

    // 400 error
    model.Components.Schemas.Add("eva_error_400", new OpenApiSchema
    {
      Type = "object",
      Required = new HashSet<string> { "Error" },
      Properties = new Dictionary<string, OpenApiSchema>
      {
        {
          "Error", new OpenApiSchema
          {
            Type = "object",
            Required = new HashSet<string> { "Message", "Type", "Code", "RequestID" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
              { "Message", new OpenApiSchema { Type = "string" } },
              { "Type", new OpenApiSchema { Type = "string" } },
              { "Code", new OpenApiSchema { Type = "string" } },
              { "RequestID", new OpenApiSchema { Type = "string" } }
            }
          }
        }
      }
    });

    model.Components.Examples.Add("eva_example_400_RequestValidationFailure", new OpenApiExample
    {
      Summary = "An example BadRequest response",
      Value = new OpenApiObject
      {
        {
          "Error", new OpenApiObject
          {
            { "Message", new OpenApiString("Validation of the request message failed: Field ABC has an invalid value for a Product") },
            { "Type", new OpenApiString("RequestValidationFailure") },
            { "Code", new OpenApiString("COVFEFE") },
            { "RequestID", new OpenApiString("576b62dd71894e3281a4d84951f44e70") }
          }
        }
      }
    });

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
          Enum = totals.Select(kv => new OpenApiInteger((int)kv.Value) as IOpenApiAny).ToList(),
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
      schema.Description = prop.Description ?? string.Empty;

      if (prop.DataModelInformation != null)
      {
        schema.Description += $"\n\nThis should be an existing ID of a {prop.DataModelInformation.Name}";
      }

      if (prop.Required is { Effective: not null } required)
      {
        schema.Description += $"\n\n**Required since {required.Introduced}:** {required.Comment}\n\n**Will be enforced in {required.Effective}**";
      }

      if (prop.StringLengthConstraint is {} slc)
      {
        schema.MinLength = slc.Min;
        schema.MaxLength = slc.Max;
        schema.Description += $"\n\nThis string must be between {slc.Min} (incl) and {slc.Max} (incl) characters long.";
      }

      if (prop.Deprecated is { } deprecated)
      {
        schema.Deprecated = true;
        schema.Description += $"\n\n**Deprecated since {deprecated.Introduced}:** {deprecated.Comment}\n\n**Will be removed in {deprecated.Effective}**";
      }

      if (prop.AllowedValues is { Length: > 0 } allowedValues)
      {
        schema.Enum = allowedValues.Select<string, IOpenApiAny>(v => new OpenApiString(v)).ToList();
        schema.Description += $"\n\nPossible values:\n\n{string.Join('\n', allowedValues.Select(v => $"* `{v}`"))}";
      }

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
          Reference = new OpenApiReference
          {
            Type = ReferenceType.Parameter,
            Id = Parameter_Header_UserAgent
          }
        },
        new()
        {
          Reference = new OpenApiReference
          {
            Type = ReferenceType.Parameter,
            Id = Parameter_Header_IdsMode
          }
        },
        new()
        {
          Reference = new OpenApiReference
          {
            Type = ReferenceType.Parameter,
            Id = Parameter_Header_AppToken
          }
        },
        new()
        {
          Reference = new OpenApiReference
          {
            Type = ReferenceType.Parameter,
            Id = Parameter_Header_ElevationToken
          }
        }
      },
      Operations = new Dictionary<OperationType, OpenApiOperation>
      {
        {
          OperationType.Post, new OpenApiOperation
          {
            Summary = service.Name,
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
                        Schema = ToSchema(service.ResponseTypeID),
                      }
                    }
                  }
                }
              },
              {
                "400", new OpenApiResponse
                {
                  Description = "A BadRequest response",
                  Content = new Dictionary<string, OpenApiMediaType>
                  {
                    {
                      "application/json", new OpenApiMediaType
                      {
                        Schema = new OpenApiSchema
                        {
                          Reference = new OpenApiReference
                          {
                            Id = "eva_error_400",
                            Type = ReferenceType.Schema
                          }
                        },
                        Examples = new Dictionary<string, OpenApiExample>
                        {
                          {
                            "RequestValidationFailure", new OpenApiExample
                            {
                              Reference = new OpenApiReference { Type = ReferenceType.Example, Id = "eva_example_400_RequestValidationFailure" }
                            }
                          }
                        }
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