using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace EVA.SDK.Generator.V2.Helpers;

public static class JsonExtensions
{
  public static ValueTask<T?> DeserializeAsync<T>(this JsonTypeInfo<T> jsonTypeInfo, Stream stream, CancellationToken cancellationToken = default)
  {
    return JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken);
  }

  public static T Clone<T>(this JsonTypeInfo<T> jsonTypeInfo, T value)
  {
    var bytes = JsonSerializer.SerializeToUtf8Bytes(value, jsonTypeInfo);
    return JsonSerializer.Deserialize(bytes, jsonTypeInfo)!;
  }
}