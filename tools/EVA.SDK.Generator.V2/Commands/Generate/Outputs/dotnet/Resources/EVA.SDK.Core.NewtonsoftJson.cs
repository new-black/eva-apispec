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

[Newtonsoft.Json.JsonConverter(typeof(OptionalConverter))]
public partial struct MaybeModelID : OptionalConverter.IGenericAccess
{
  Type OptionalConverter.IGenericAccess.GenericType => _valueIsPresent && _value.IsLong ? typeof(long) : typeof(string);
  bool OptionalConverter.IGenericAccess.IsValuePresent => _valueIsPresent;

  object OptionalConverter.IGenericAccess.Value
  {
    set
    {
      _value = (ModelID)value;
      _valueIsPresent = true;
    }
    get => _value;
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

[Newtonsoft.Json.JsonConverter(typeof(ModelID.Converter))]
public partial struct ModelID
{
  private class Converter : Newtonsoft.Json.JsonConverter
  {
    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
    {
      var ls = (ModelID)value;

      if(ls.IsLong)
        writer.WriteValue(ls.Long);
      else
        writer.WriteValue(ls.String);
    }

    public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
      if(reader.TokenType == Newtonsoft.Json.JsonToken.Integer)
        return new ModelID((long)reader.Value);
      if(reader.TokenType == Newtonsoft.Json.JsonToken.String)
        return new ModelID((string)reader.Value);
      if(reader.TokenType == Newtonsoft.Json.JsonToken.Null)
        return new ModelID((string) null);

      return existingValue;
    }

    public override bool CanConvert(Type objectType) => true;
  }
}

[Newtonsoft.Json.JsonConverter(typeof(ModelIDList.Converter))]
public partial class ModelIDList
{
  private class Converter : Newtonsoft.Json.JsonConverter<ModelIDList>
  {
    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, ModelIDList value, Newtonsoft.Json.JsonSerializer serializer)
    {
      serializer.Serialize(writer, value._ids);
    }

    public override ModelIDList ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, ModelIDList existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
      var values = serializer.Deserialize<List<ModelID>>(reader);
      return values == null ? null : new ModelIDList(values);
    }
  }
}