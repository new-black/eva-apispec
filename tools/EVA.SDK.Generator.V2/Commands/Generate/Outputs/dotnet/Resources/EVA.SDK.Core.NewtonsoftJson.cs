using EVA.SDK.Core;
using Newtonsoft.Json;

namespace EVA.SDK;

public class MaybeConverter<T> : Newtonsoft.Json.JsonConverter<Maybe<T>>
{
  public override void WriteJson(JsonWriter writer, Maybe<T> value, JsonSerializer serializer)
  {
    serializer.Serialize(writer, value.Value);
  }

  public override Maybe<T> ReadJson(JsonReader reader, Type objectType, Maybe<T> existingValue, bool hasExistingValue, JsonSerializer serializer)
  {
    if (reader.TokenType != Newtonsoft.Json.JsonToken.Undefined)
    {
      existingValue.Value = serializer.Deserialize<T>(reader);
    }

    return existingValue;
  }
}