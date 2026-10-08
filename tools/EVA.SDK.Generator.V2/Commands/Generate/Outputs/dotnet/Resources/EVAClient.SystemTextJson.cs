using System.IO;
using System.Text.Json;

namespace EVA.SDK;

public partial class EVAApiClient<TState, TServiceCallOptions>
{
  private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
  {
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    Converters = { new UtcDateTimeConverter() }
  };

  private partial void SerializeRequest(Stream stream, object requestMessage)
  {
    JsonSerializer.Serialize(stream, requestMessage, requestMessage.GetType(), _jsonSerializerOptions);
  }

  private partial TResponse DeserializeResponse<TResponse>(Stream stream)
  {
    return JsonSerializer.Deserialize<TResponse>(stream, _jsonSerializerOptions)!;
  }
}