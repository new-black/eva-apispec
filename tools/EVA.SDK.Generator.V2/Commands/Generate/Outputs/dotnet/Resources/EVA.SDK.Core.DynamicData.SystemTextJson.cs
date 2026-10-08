using System.Text.Json;
using System.Text.Json.Nodes;

namespace EVA.SDK;

public partial class EVAApiClient<TState, TServiceCallOptions>
{
  partial void Setup()
  {
    foreach (var c in DynamicDataConverters.Converters)
    {
      _jsonSerializerOptions.Converters.Add(c);
    }
  }
}

public class DynamicDataConverter<T> : System.Text.Json.Serialization.JsonConverter<DynamicData<T>> where T : class
{
  public override DynamicData<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Null) return null;

    var node = JsonNode.Parse(ref reader) as JsonObject;
    if (node == null) return null;

    return new DynamicData<T> { Data = node };
  }

  public override void Write(Utf8JsonWriter writer, DynamicData<T> value, JsonSerializerOptions options)
  {
    value.Data.WriteTo(writer, options);
  }
}

public partial class DynamicData<TShared>
{
  public TShared? Shared => Data.Deserialize<TShared>()!;

  public JsonObject Data { get; set; }

  public static implicit operator DynamicData<TShared>?(TShared? x)
  {
    return x == null ? null : new DynamicData<TShared> { Data = (JsonObject)JsonSerializer.SerializeToNode(x)! };
  }

  public static implicit operator DynamicData<TShared>(JsonObject x)
  {
    return x == null ? null : new DynamicData<TShared> { Data = x };
  }
}

public static class DynamicDataExtensions {
  public static DynamicData<TShared>? AsDynamic<TShared>(this TShared? data) where TShared : class {
    return data == null ? null : new DynamicData<TShared> { Data = (JsonObject)JsonSerializer.SerializeToNode(data)! };
  }
}
