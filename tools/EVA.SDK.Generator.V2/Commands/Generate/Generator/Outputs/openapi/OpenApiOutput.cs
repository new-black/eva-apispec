using System.Collections.Immutable;
using System.Text.RegularExpressions;
using EVA.API.Spec;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Services;
using Microsoft.OpenApi.Writers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.openapi;

public class OpenApiOutput : IOutput
{
  private readonly OpenApiOptions _options;

  public OpenApiOutput(OpenApiOptions options)
  {
    _options = options;
  }

  public void FixOptions(GenerateOptions options)
  {
    options.EnsureRemove("generics");
    options.EnsureRemove("unused-type-params");

    if (_options.Preset == "azure-connector")
    {
      options.EnsureRemove("inheritance");
      _options.Terse = true;
      _options.Format = "json";
      _options.Version = "v2";
    }
  }

  public async Task Write(ApiDefinitionModel input, string outputDirectory)
  {
    var outputPath = Path.GetFullPath(Path.Combine(outputDirectory, "openapi.json"));
    Console.WriteLine($"Writing OpenAPI file: {outputPath}");

    var model = GetModel(input);

    var version = _options.Version == "v2" ? OpenApiSpecVersion.OpenApi2_0 : OpenApiSpecVersion.OpenApi3_0;

    await using var file = File.OpenWrite(outputPath);
    await using var textWriter = new StreamWriter(file);
    IOpenApiWriter writer = _options.Format == "yaml"
      ? new OpenApiYamlWriter(textWriter, new OpenApiWriterSettings())
      : new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings { Terse = _options.Terse });

    model.Serialize(writer, version);
  }

  private OpenApiDocument GetModel(ApiDefinitionModel input)
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
        new OpenApiServer { Url = _options.Host }
      },
      Paths = new OpenApiPaths(),
      Components = new OpenApiComponents { },
      SecurityRequirements = new List<OpenApiSecurityRequirement>
      {
        new OpenApiSecurityRequirement
        {
          { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "eva-auth" } }, new List<string>() }
        }
      }
    };

    if (_options.Preset == "azure-connector")
    {
      model.Components.SecuritySchemes.Add("eva-auth", new OpenApiSecurityScheme
      {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
          AuthorizationCode = new OpenApiOAuthFlow
          {
            AuthorizationUrl = new Uri("https://henk2.platform-tools.on-eva.io/ui/auth/azure-connector"),
            TokenUrl = new Uri("https://henk2.platform-tools.on-eva.io/api/auth/azure-connector"),
            RefreshUrl = new Uri("https://henk2.platform-tools.on-eva.io/api/auth/azure-connector/refresh"),
            Scopes = new Dictionary<string, string>()
          }
        }
      });
    }
    else
    {
      model.Components.SecuritySchemes.Add("eva-auth", new OpenApiSecurityScheme
      {
        Description = "The default authentication mechanism when communicating with EVA",
        Type = SecuritySchemeType.ApiKey,
        Name = "Authorization",
        In = ParameterLocation.Header
      });
    }

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

    if (_options.Preset == "azure-connector")
    {
      foreach (var eventExportType in input.EventTargets)
      {
        var properties = new Dictionary<string, OpenApiSchema>
        {
          { "UID", new OpenApiSchema { Type = "string" } },
          { "CreationTime", new OpenApiSchema { Type = "string" } },
          { "TimeZone", new OpenApiSchema { Type = "string" } },
          { "Target", new OpenApiSchema { Type = "string" } },
          { "EventType", new OpenApiSchema { Type = "string" } },
          { "Identifier", new OpenApiSchema { Type = "string" } },
          { "BackendID", new OpenApiSchema { Type = "string" } },
          { "BackendSystemID", new OpenApiSchema { Type = "string" } },
          { "Region", new OpenApiSchema { Type = "string" } },
          { "Attempt", new OpenApiSchema { Type = "number" } },
          { "Environment", new OpenApiSchema { Type = "string" } }
        };

        if (eventExportType.DataType != null)
        {
          properties.Add("Data", new OpenApiSchema { Type = "object" });
        }

        // Render the trigger link
        model.Paths.Add($"/api/azure-connector/subscribe/{eventExportType.Target}/{eventExportType.Type}", new OpenApiPathItem
        {
          Extensions = new Dictionary<string, IOpenApiExtension>
          {
            {
              "x-ms-notification-content", new ExtensionNotification(new OpenApiSchema
              {
                Type = "object",
                Properties = properties
              }, $"{eventExportType.Target} {SnakeCaseToPascalCase(eventExportType.Type)}")
            }
          },
          Parameters = new List<OpenApiParameter>
          {
            new OpenApiParameter
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
                Summary = $"EVA Event - {eventExportType.Target} {SnakeCaseToPascalCase(eventExportType.Type)}",
                Description = $"EVA Event - {eventExportType.Target} {SnakeCaseToPascalCase(eventExportType.Type)}",
                OperationId = $"AzureConnectorSubscribe{eventExportType.Target}{SnakeCaseToPascalCase(eventExportType.Type)}",
                Tags = new List<OpenApiTag> { new OpenApiTag { Name = "Technical" } },
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                  { "x-ms-trigger", new ExtensionString("single") }
                },
                Security = new List<OpenApiSecurityRequirement>
                {
                  new OpenApiSecurityRequirement
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
                  Required = true,
                  Content = new Dictionary<string, OpenApiMediaType>
                  {
                    {
                      "application/json", new OpenApiMediaType
                      {
                        Schema = new OpenApiSchema
                        {
                          Type = "object",
                          Required = new HashSet<string> { "CallbackUrl" },
                          Properties = new Dictionary<string, OpenApiSchema>
                          {
                            {
                              "CallbackUrl", new OpenApiSchema
                              {
                                Type = "string",
                                Title = "",
                                Description = "The callback url",
                                Extensions = new Dictionary<string, IOpenApiExtension>
                                {
                                  { "x-ms-notification-url", new ExtensionBool(true) },
                                  { "x-ms-visibility", new ExtensionString("internal") }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                },
                Responses = new OpenApiResponses
                {
                  {
                    "200", new OpenApiResponse
                    {
                      Description = "Default response",
                      Content = new Dictionary<string, OpenApiMediaType>
                      {
                        {
                          "application/json", new OpenApiMediaType
                          {
                            Schema = new OpenApiSchema { Type = "object" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        });
      }
    }

    return model;
  }

  private OpenApiSchema ToSchema(ApiDefinitionModel input, TypeSpecification type)
  {
    var result = new OpenApiSchema
    {
      Type = "object",
      AdditionalPropertiesAllowed = false,
      Properties = new Dictionary<string, OpenApiSchema>()
    };

    if (type.Properties != null)
    {
      foreach (var (name, prop) in type.Properties)
      {
        var schema = ToSchema(input, prop.Type);
        if (schema != null)
        {
          schema.Description = prop.Description;
          result.Properties.Add(name, schema);
        }
      }

      result.Required = type.Properties.Where(p => !p.Value.Type.Nullable).Select(p => p.Key).OrderBy(x => x).ToImmutableHashSet();
    }

    if (type.Extends != null)
    {
      result.AllOf = new List<OpenApiSchema> { ToSchema(input, type.Extends) };
    }

    return result;
  }

  private OpenApiSchema? ToSchema(ApiDefinitionModel input, TypeReference type)
  {
    if (type.Name == "int16") return new OpenApiSchema { Type = "integer" };
    if (type.Name == "int32") return new OpenApiSchema { Type = "integer" };
    if (type.Name == "int64") return new OpenApiSchema { Type = "integer" };
    if (type.Name == "string") return new OpenApiSchema { Type = "string" };
    if (type.Name == "bool") return new OpenApiSchema { Type = "boolean" };
    if (type.Name == "guid") return new OpenApiSchema { Type = "string", Format = "uuid" };
    if (type.Name == "float32") return new OpenApiSchema { Type = "number" };
    if (type.Name == "float64") return new OpenApiSchema { Type = "number" };
    if (type.Name == "float128") return new OpenApiSchema { Type = "number" };
    if (type.Name == "duration") return new OpenApiSchema { Type = "string" };
    if (type.Name == "object") return new OpenApiSchema { Type = "object", AdditionalPropertiesAllowed = true };
    if (type.Name == "any") return new OpenApiSchema { Type = "object", AdditionalPropertiesAllowed = true };
    if (type.Name == "date") return new OpenApiSchema { Type = "string", Format = "date-time" };
    if (type.Name == "array") return new OpenApiSchema { Type = "array", Items = ToSchema(input, type.Arguments.Single()) };
    if (type.Name == "option") return new OpenApiSchema { AnyOf = type.Arguments.Select(a => ToSchema(input, a)).ToList() };

    if (type.Name == "map")
    {
      var keyType = type.Arguments[0].Name;
      if (keyType is "string" or "int64" or "float128" or "date" || (char.IsUpper(keyType[0]) && input.Types[keyType].EnumIsFlag.HasValue))
      {
        return new OpenApiSchema
        {
          Type = "object",
          AdditionalPropertiesAllowed = true,
          AdditionalProperties = ToSchema(input, type.Arguments[1])
        };
      }
    }

    if (!type.Name.StartsWith("_") && (type.Arguments == null || !type.Arguments.Any()))
    {
      return ToSchema(input, type.Name);
    }

    //Console.WriteLine("Saw: " + type.Name);
    throw new Exception(type.Name);
    return null;
  }

  private OpenApiPathItem ToPathItem(ApiDefinitionModel input, ServiceModel service)
  {
    return new OpenApiPathItem
    {
      Summary = service.Name,
      Description = input.Types[service.RequestTypeID].Description ?? $"The {service.Name} service",
      Parameters = new List<OpenApiParameter>
      {
        new OpenApiParameter
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
            Summary = service.Name,
            Description = input.Types[service.RequestTypeID].Description ?? $"The {service.Name} service",
            OperationId = service.Name,
            Tags = new List<OpenApiTag> { new OpenApiTag { Name = TagFromAssembly(service.Assembly), Description = TagFromAssembly(service.Assembly) } },
            Security = new List<OpenApiSecurityRequirement>
            {
              new OpenApiSecurityRequirement
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
              Description = input.Types[service.RequestTypeID].Description ?? $"A request to {service.Name}",
              Required = true,
              Content = new Dictionary<string, OpenApiMediaType>
              {
                {
                  "application/json", new OpenApiMediaType
                  {
                    Schema = ToSchema(input, service.RequestTypeID)
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
                        Schema = ToSchema(input, service.ResponseTypeID)
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

  private OpenApiSchema ToSchema(ApiDefinitionModel input, string id)
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

  private static readonly Regex _nameRegex = new Regex("[^a-zA-Z0-9-._]", RegexOptions.Compiled);

  private string FixName(string name)
  {
    return _nameRegex.Replace(name, "_").Trim('_');
  }

  private string TagFromAssembly(string name)
  {
    return name.StartsWith("EVA.") ? name[4..] : name;
  }


  private class ExtensionBool : IOpenApiExtension
  {
    private readonly bool _value;

    public ExtensionBool(bool value)
    {
      _value = value;
    }

    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
      writer.WriteValue(_value);
    }
  }

  private class ExtensionString : IOpenApiExtension
  {
    private readonly string _value;

    public ExtensionString(string value)
    {
      _value = value;
    }

    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
      writer.WriteValue(_value);
    }
  }

  private class ExtensionNotification : IOpenApiExtension
  {
    private readonly OpenApiSchema _schema;
    private readonly string? _description;

    public ExtensionNotification(OpenApiSchema schema, string? description = null)
    {
      _schema = schema;
      _description = description;
    }

    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
      writer.WriteStartObject();
      writer.WritePropertyName("schema");
      _schema.Serialize(writer, specVersion);
      if (_description != null)
      {
        writer.WritePropertyName("description");
        writer.WriteValue(_description);
      }

      writer.WriteEndObject();
    }
  }

  private static string SnakeCaseToPascalCase(string s)
  {
    var q = s.Split('_').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => char.ToUpperInvariant(x[0]) + x[1..]);
    return string.Join("", q);
  }
}