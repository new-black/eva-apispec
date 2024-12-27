using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EVA.SDK;

public partial class EVAApiClient<TState, TServiceCallOptions>
{
  partial void Setup()
  {
    foreach (var c in DynamicDataConverters.Converters)
    {
      _newtonsoftJsonSerializer.Converters.Add(c);
    }
  }
}

public class DynamicDataConverter<T> : Newtonsoft.Json.JsonConverter<DynamicData<T>> where T : class
{
  public override void WriteJson(JsonWriter writer, DynamicData<T>? value, JsonSerializer serializer)
  {
    serializer.Serialize(writer, value?.Data);
  }

  public override DynamicData<T>? ReadJson(JsonReader reader, Type objectType, DynamicData<T>? existingValue, bool hasExistingValue, JsonSerializer serializer)
  {
    if (reader.TokenType is JsonToken.Null or JsonToken.Undefined) return existingValue;

    existingValue ??= new DynamicData<T>();
    existingValue.Data = serializer.Deserialize<JObject>(reader);
    return existingValue;
  }
}

public partial class DynamicData<TShared>
{
  public TShared? Shared => Data.ToObject<TShared>()!;

  public Newtonsoft.Json.Linq.JObject Data { get; set; }

  public static implicit operator DynamicData<TShared>?(TShared? x)
  {
    return x == null ? null : new DynamicData<TShared> { Data = Newtonsoft.Json.Linq.JObject.FromObject(x) };
  }

  public static implicit operator DynamicData<TShared>(Newtonsoft.Json.Linq.JObject x)
  {
    return x == null ? null : new DynamicData<TShared> { Data = x };
  }
}

public static class DynamicDataExtensions {
  public static DynamicData<TShared>? AsDynamic<TShared>(this TShared? data) where TShared : class {
    return data == null ? null : new DynamicData<TShared> { Data = Newtonsoft.Json.Linq.JObject.FromObject(data) };
  }
}