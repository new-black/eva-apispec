using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;

/// <summary>
/// The names of the EVA extensions in the OpenAPI document. See README.md of EVA.OpenAPI.Generator for their meaning.
/// </summary>
internal static class ExtensionNames
{
  internal const string ApiVersion = "x-eva-api-version";
  internal const string Errors = "x-eva-errors";

  internal const string ID = "x-eva-id";
  internal const string Name = "x-eva-name";
  internal const string Assembly = "x-eva-assembly";
  internal const string Parent = "x-eva-parent";
  internal const string TypeParameters = "x-eva-type-parameters";
  internal const string Template = "x-eva-template";
  internal const string FlagValues = "x-eva-flag-values";
  internal const string EnumNames = "x-enum-varnames";
  internal const string EnumDescriptions = "x-enum-descriptions";

  internal const string TypeParameter = "x-eva-type-parameter";
  internal const string TypeArguments = "x-eva-type-arguments";
  internal const string Option = "x-eva-option";
  internal const string MapKey = "x-eva-map-key";

  internal const string Skippable = "x-eva-skippable";
  internal const string Deprecated = "x-eva-deprecated";
  internal const string Required = "x-eva-required";
  internal const string Minimum = "x-eva-minimum";
  internal const string Maximum = "x-eva-maximum";
  internal const string Entity = "x-eva-entity";

  internal const string Auth = "x-eva-auth";
  internal const string Async = "x-eva-async";
}

/// <summary>
/// An extension that holds a schema, like the parent of a nested type or the key of a map.
/// </summary>
internal sealed class SchemaExtension(IOpenApiSchema schema) : IOpenApiExtension
{
  internal IOpenApiSchema Schema { get; set; } = schema;

  public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion) => Schemas.Serialize(Schema, writer, specVersion);
}

/// <summary>
/// An extension that holds a list of schemas, like the type arguments of a generic type.
/// </summary>
internal sealed class SchemaListExtension(List<IOpenApiSchema> schemas) : IOpenApiExtension
{
  internal List<IOpenApiSchema> Schemas { get; } = schemas;

  public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
  {
    writer.WriteStartArray();
    foreach (var schema in Schemas) Model.Schemas.Serialize(schema, writer, specVersion);
    writer.WriteEndArray();
  }
}

/// <summary>
/// The extension that marks an option, holding the type all of its implementations share.
/// </summary>
internal sealed class OptionExtension(IOpenApiSchema shared) : IOpenApiExtension
{
  internal IOpenApiSchema Shared { get; set; } = shared;

  public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
  {
    writer.WriteStartObject();
    writer.WritePropertyName("shared");
    Schemas.Serialize(Shared, writer, specVersion);
    writer.WriteEndObject();
  }
}

/// <summary>
/// The errors of the API.
/// </summary>
internal sealed class ErrorsExtension(List<EvaError> errors) : IOpenApiExtension
{
  internal List<EvaError> Errors { get; } = errors;

  public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
  {
    writer.WriteStartArray();
    foreach (var error in Errors)
    {
      writer.WriteStartObject();
      writer.WriteProperty("name", error.Name);
      writer.WriteProperty("message", error.Message);
      writer.WriteProperty("assembly", error.Assembly);
      writer.WritePropertyName("arguments");
      writer.WriteStartArray();
      foreach (var argument in error.Arguments)
      {
        writer.WriteStartObject();
        if (argument.Name != null) writer.WriteProperty("name", argument.Name);
        writer.WritePropertyName("schema");
        Schemas.Serialize(argument.Schema, writer, specVersion);
        writer.WriteEndObject();
      }

      writer.WriteEndArray();
      writer.WriteEndObject();
    }

    writer.WriteEndArray();
  }
}
