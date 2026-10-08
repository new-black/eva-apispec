using EVA.SDK.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EVA.SDK;

public class MaybeConverter<T> : JsonConverter<Maybe<T>>
{
  /// <summary>
  /// Maybe semantics: no value = the property is omitted on write (see the
  /// [JsonIgnore] condition), null = the property is written as null, a value =
  /// the property is written with that value. A null token is therefore still
  /// a present value.
  /// </summary>
  public override bool HandleNull => true;

  public override Maybe<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Null)
    {
      return new Maybe<T> { Value = default! };
    }

    return new Maybe<T> { Value = JsonSerializer.Deserialize<T>(ref reader, options)! };
  }

  public override void Write(Utf8JsonWriter writer, Maybe<T> value, JsonSerializerOptions options)
  {
    JsonSerializer.Serialize(writer, value.Value, options);
  }
}

/// <summary>
/// Reads/writes dates as UTC, matching the Newtonsoft client's
/// DateTimeZoneHandling.Utc (unspecified kinds are treated as UTC).
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
  public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    var value = reader.GetDateTime();
    return value.Kind == DateTimeKind.Unspecified
      ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
      : value.ToUniversalTime();
  }

  public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
  {
    var utc = value.Kind == DateTimeKind.Unspecified
      ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
      : value.ToUniversalTime();
    writer.WriteStringValue(utc);
  }
}
