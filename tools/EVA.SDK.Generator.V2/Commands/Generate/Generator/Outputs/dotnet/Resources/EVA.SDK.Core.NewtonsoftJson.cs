[Newtonsoft.Json.JsonConverter(typeof(OptionalConverter))]
public struct Maybe<T> : OptionalConverter.IGenericAccess
{
  private bool _isValuePresent;
  private T _value;

  public bool IsValuePresent
  {
    get => _isValuePresent;
    set
    {
      _isValuePresent = value;

      if (!value)
      {
        Value = default;
      }
    }
  }

  public T Value
  {
    get => _value;
    set
    {
      _value = value;
      _isValuePresent = true;
    }
  }

  Type OptionalConverter.IGenericAccess.GenericType => typeof(T);

  object OptionalConverter.IGenericAccess.Value
  {
    set => Value = (T)value;
    get => Value;
  }

  public static implicit operator Maybe<T>(T value) => new Maybe<T> { Value = value };

  public static explicit operator T(Maybe<T> maybe)
  {
    if (!maybe.IsValuePresent)
    {
      throw new InvalidOperationException("The optional value is not preset");
    }

    return maybe.Value;
  }

  public override string ToString()
  {
    if (!IsValuePresent) return "{no value}";
    if (Value == null) return "{null}";
    return Value.ToString();
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