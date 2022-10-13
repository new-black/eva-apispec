using System.Text.Json.Serialization;
using EVA.Core.Typings.V2;
using EVA.SDK.Generator.V2.Generator.Inputs;

namespace EVA.SDK.Generator.V2.Helpers;

[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ApiDefinitionModel))]
[JsonSerializable(typeof(HttpInput.Response))]
[JsonSerializable(typeof(HttpInput.Request))]
internal partial class NonIndentedSerializationHelper : JsonSerializerContext
{

}

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ServiceModel))]
[JsonSerializable(typeof(TypeSpecification))]
[JsonSerializable(typeof(TypeReference))]
internal partial class IndentedSerializationHelper : JsonSerializerContext
{

}