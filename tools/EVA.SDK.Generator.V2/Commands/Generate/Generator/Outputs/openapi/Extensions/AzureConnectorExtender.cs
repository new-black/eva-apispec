using System.Collections.Immutable;
using EVA.API.Spec;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.openapi.Extensions;

public class AzureConnectorExtender
{
  public static void Extend(OpenApiDocument model, ApiDefinitionModel input)
  {
    // Replace auth method
    model.Components.SecuritySchemes.Remove("eva-auth");
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

    // Delete endpoint
    model.Paths.Add("/api/azure-connector/unsubscribe/{subscriptionID}", new OpenApiPathItem
    {
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
        },
        new OpenApiParameter
        {
          In = ParameterLocation.Path,
          Name = "subscriptionID",
          Required = true,
          Schema = new OpenApiSchema { Type = "string" }
        }
      },
      Operations = new Dictionary<OperationType, OpenApiOperation>
      {
        {
          OperationType.Delete, new OpenApiOperation
          {
            Summary = $"Delete a trigger",
            Description = $"Delete a trigger",
            OperationId = $"AzureConnectorUnsubscribe",
            Tags = new List<OpenApiTag> { new() { Name = "Technical" } },
            Security = new List<OpenApiSecurityRequirement>
            {
              new()
              {
                { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "eva-auth" } }, ImmutableList<string>.Empty }
              }
            }
          }
        }
      }
    });

    // Event export endpoints
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
              Tags = new List<OpenApiTag> { new() { Name = "Technical" } },
              Extensions = new Dictionary<string, IOpenApiExtension> { { "x-ms-trigger", new ExtensionString("single") } },
              Security = new List<OpenApiSecurityRequirement>
              {
                new()
                {
                  { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "eva-auth" } }, ImmutableList<string>.Empty }
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