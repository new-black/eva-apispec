using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace EVA.SDK;

public partial class EVAApiClient<TState, TServiceCallOptions>
{
  private readonly Encoding _encoding = new UTF8Encoding(false);

  private readonly JsonSerializer _newtonsoftJsonSerializer = new JsonSerializer
  {
    DateFormatHandling = DateFormatHandling.IsoDateFormat,
    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
    NullValueHandling = NullValueHandling.Ignore
  };

  private partial void SerializeRequest(Stream stream, object requestMessage)
  {
    using var writer = new StreamWriter(stream, _encoding, leaveOpen: true);
    _newtonsoftJsonSerializer.Serialize(writer, requestMessage);
  }

  private partial TResponse DeserializeResponse<TResponse>(Stream stream)
  {
    using var reader = new StreamReader(stream);
    using var jsonTextReader = new JsonTextReader(reader);
    return _newtonsoftJsonSerializer.Deserialize<TResponse>(jsonTextReader)!;
  }
}