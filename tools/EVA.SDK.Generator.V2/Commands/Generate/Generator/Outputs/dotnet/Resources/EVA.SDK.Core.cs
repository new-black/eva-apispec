public interface IResponseMessage
{
  public ServiceError? Error { get; }
}

public interface IResponseType<out TResponse> where TResponse : IResponseMessage
{

}

public class RequestMessageAttribute : Attribute
{
  public string Path { get; }

  public RequestMessageAttribute(string path)
  {
    Path = path;
  }
}

public class PageConfig : EVA.SDK.Core.PageConfig<IDictionary<string?,string?>?> {

}

public partial struct LongOrString
{
  private readonly long? _long;
  private readonly string? _string;

  public bool IsLong => _long.HasValue;
  public bool IsString => _string != null;

  public long Long => _long.HasValue ? _long.Value : throw new InvalidOperationException("LongOrString is not a long");
  public string String => _string != null ? _string : throw new InvalidOperationException("LongOrString is not a string");

  public LongOrString(long value)
  {
    _long = value;
    _string = null;
  }

  public LongOrString(string value)
  {
    _long = null;
    _string = value;
  }

  public static implicit operator LongOrString(long value) => new LongOrString(value);
  public static implicit operator LongOrString(string value) => new LongOrString(value);

  public static implicit operator long(LongOrString value) => value.Long;
  public static implicit operator string(LongOrString value) => value.String;

  public override string ToString() => IsLong ? Long.ToString() : String;

  public override bool Equals(object? obj) => obj is LongOrString other && Equals(other);

  public bool Equals(LongOrString other) => IsLong == other.IsLong && IsString == other.IsString && (IsLong ? Long == other.Long : String == other.String);

  public override int GetHashCode() => IsLong ? Long.GetHashCode() : String.GetHashCode();

  public static bool operator ==(LongOrString left, LongOrString right) => left.Equals(right);

  public static bool operator !=(LongOrString left, LongOrString right) => !left.Equals(right);
}

public partial struct Maybe<T>
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