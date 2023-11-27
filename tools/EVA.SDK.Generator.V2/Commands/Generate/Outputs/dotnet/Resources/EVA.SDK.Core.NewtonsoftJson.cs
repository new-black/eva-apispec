public class SkipMaybePropertiesContractResolver : DefaultContractResolver
{
  protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
  {
    var property = base.CreateProperty(member, memberSerialization);
    if ((property.PropertyType?.IsGenericType ?? false) && property.PropertyType.GetGenericTypeDefinition() == typeof(Maybe<>))
    {
      property.ShouldSerialize = o => ((OptionalConverter.IGenericAccess)property.ValueProvider?.GetValue(o))?.IsValuePresent ?? false;
    }

    return property;
  }
}

[Newtonsoft.Json.JsonConverter(typeof(OptionalConverter))]
public partial struct Maybe<T> : OptionalConverter.IGenericAccess
{
  Type OptionalConverter.IGenericAccess.GenericType => typeof(T);

  object OptionalConverter.IGenericAccess.Value
  {
    set => Value = (T)value;
    get => Value;
  }
}

public class OptionalConverter : Newtonsoft.Json.JsonConverter
{
  public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
  {
    var genericAccess = (IGenericAccess)value;
    serializer.Serialize(writer, genericAccess.Value);
  }

  public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
  {
    existingValue ??= Activator.CreateInstance(objectType);

    if (reader.TokenType != Newtonsoft.Json.JsonToken.Undefined)
    {
      var genericAccess = (IGenericAccess)existingValue;
      genericAccess.Value = serializer.Deserialize(reader, genericAccess.GenericType);
    }

    return existingValue;
  }

  public override bool CanConvert(Type objectType) => true;

  public interface IGenericAccess
  {
    bool IsValuePresent { get; }
    Type GenericType { get; }
    object Value { set; get; }
  }
}

public class DynamicDataConverter : Newtonsoft.Json.JsonConverter
{
  public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
  {
    serializer.Serialize(writer, (value as IDynamicData)?.Data);
  }

  public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
  {
    if(reader.TokenType is JsonToken.Null or JsonToken.Undefined) return existingValue;

    existingValue ??= Activator.CreateInstance(objectType);
    (existingValue as IDynamicData).Data = serializer.Deserialize<JObject>(reader);
    return existingValue;
  }

  public override bool CanConvert(Type objectType) => objectType.IsConstructedGenericType && objectType.GetGenericTypeDefinition() == typeof(DynamicData<>);
}

[Newtonsoft.Json.JsonConverter(typeof(DynamicDataConverter))]
public partial class DynamicData<TShared>
{
  public static implicit operator DynamicData<TShared>(Newtonsoft.Json.Linq.JObject x)
  {
    return x == null ? null : new DynamicData<TShared> { Data = x };
  }

  public T? Value<T>(string key) => Data == null ? default(T?) : Data.Value<T>(key);
  public virtual JToken? this[object key] => Data == null ? null : Data[key];

  public JToken? GetValue(string? propertyName)
  {
    return Data?.GetValue(propertyName);
  }

  public bool HasValue() => Data != null && Data.Count > 0;
}