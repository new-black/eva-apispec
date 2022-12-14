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
    if (!genericAccess.IsValuePresent)
    {
      writer.WriteUndefined();
      return;
    }

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

[Newtonsoft.Json.JsonConverter(typeof(LongOrStringConverter))]
public partial struct LongOrString
{

}

public class LongOrStringConverter : Newtonsoft.Json.JsonConverter
{
  public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
  {
    var ls = (LongOrString)value;

    if(ls.IsLong)
      writer.WriteValue(ls.Long);
    else
      writer.WriteValue(ls.String);
  }

  public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
  {
    if(reader.TokenType == Newtonsoft.Json.JsonToken.Integer)
      return new LongOrString((long)reader.Value);
    if(reader.TokenType == Newtonsoft.Json.JsonToken.String)
      return new LongOrString((string)reader.Value);
    if(reader.TokenType == Newtonsoft.Json.JsonToken.Null)
      return new LongOrString((string) null);

    return existingValue;
  }

  public override bool CanConvert(Type objectType) => true;
}