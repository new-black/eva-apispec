using System.Text.Json.Serialization;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Generator.Inputs;
using EVA.SDK.Generator.V2.Commands.Update;

namespace EVA.SDK.Generator.V2.Commands.Generate.Helpers;

[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ApiDefinitionModel))]
[JsonSerializable(typeof(HttpInput.Response))]
[JsonSerializable(typeof(UpdateCommand.LatestResponse))]
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