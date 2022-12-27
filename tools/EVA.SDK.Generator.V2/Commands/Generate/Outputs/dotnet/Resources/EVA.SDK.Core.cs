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

public partial class ModelIDList : IEnumerable<ModelID>,IEnumerable<long>,IEnumerable<string>
{
  private readonly List<ModelID> _ids;

  public ModelIDList(List<ModelID> ids) { _ids = ids; }

  IEnumerator<ModelID> IEnumerable<ModelID>.GetEnumerator() => _ids.GetEnumerator();
  IEnumerator<long> IEnumerable<long>.GetEnumerator() => _ids.Select(v => v.Long).GetEnumerator();
  IEnumerator<string> IEnumerable<string>.GetEnumerator() => _ids.Select(v => v.String).GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => _ids.GetEnumerator();

  public void Add(long id) => _ids.Add(id);
  public void Add(string id) => _ids.Add(id);

  public static implicit operator ModelIDList(List<long> value) => value.Select(v => new ModelID(v)).ToList();
  public static implicit operator ModelIDList(List<string> value) => value.Select(v => new ModelID(v)).ToList();
  public static implicit operator ModelIDList(List<ModelID> value) => new(value);

  public static implicit operator ModelIDList(long[] value) => value.Select(v => new ModelID(v)).ToList();
  public static implicit operator ModelIDList(string[] value) => value.Select(v => new ModelID(v)).ToList();
  public static implicit operator ModelIDList(ModelID[] value) => new(new List<ModelID>(value));

  public static implicit operator List<long>(ModelIDList value) => value._ids.Select(v => v.Long).ToList();
  public static implicit operator List<string>(ModelIDList value) => value._ids.Select(v => v.String).ToList();
}

public static class ModelIDListExtensions
{
  public static ModelIDList ToModelIDList(this IEnumerable<long> value) => value.Select(v => new ModelID(v)).ToList();
  public static ModelIDList ToModelIDList(this IEnumerable<string> value) => value.Select(v => new ModelID(v)).ToList();

  public static ModelIDList ToList(this IEnumerable<ModelID> values) => new ModelIDList(Enumerable.ToList(values));
}

public partial struct ModelID
{
  private readonly long? _long;
  private readonly string? _string;

  public bool IsLong => _long.HasValue;
  public bool IsString => _string != null;

  public long Long => _long.HasValue ? _long.Value : throw new InvalidOperationException("ModelID is not a long");
  public string String => !_long.HasValue ? _string : throw new InvalidOperationException("ModelID is not a string");

  public ModelID(long value)
  {
    _long = value;
    _string = null;
  }

  public ModelID(string value)
  {
    _long = null;
    _string = value;
  }

  public static implicit operator long(ModelID value) => value.Long;
  public static implicit operator long?(ModelID value) => value.Long;
  public static implicit operator string(ModelID value) => value.String;

  public static implicit operator ModelID(long value) => new ModelID(value);
  public static implicit operator ModelID(string value) => new ModelID(value);

  public override string ToString() => IsLong ? Long.ToString() : String;

  public override bool Equals(object? obj) => obj is ModelID other && Equals(other);

  public bool Equals(ModelID other) => IsLong == other.IsLong && IsString == other.IsString && (IsLong ? Long == other.Long : String == other.String);

  public override int GetHashCode() => IsLong ? Long.GetHashCode() : String.GetHashCode();

  public static bool operator ==(ModelID left, ModelID right) => left.Equals(right);

  public static bool operator !=(ModelID left, ModelID right) => !left.Equals(right);
}

public partial struct MaybeModelID
{
  internal bool _valueIsPresent;
  internal ModelID _value;

  private MaybeModelID(bool isValuePresent, ModelID value)
  {
    _valueIsPresent = isValuePresent;
    _value = value;
  }

  public static implicit operator MaybeModelID(long value) => new MaybeModelID(true, value);
  public static implicit operator MaybeModelID(string value) => new MaybeModelID(true, value);
  public static implicit operator MaybeModelID(ModelID value) => new MaybeModelID(true, value);

  public static implicit operator MaybeModelID(Maybe<long> value) => new MaybeModelID(value.IsValuePresent, value.IsValuePresent ? value.Value : default);
  public static implicit operator MaybeModelID(Maybe<string> value) => new MaybeModelID(value.IsValuePresent, value.IsValuePresent ? value.Value : default);
  public static implicit operator MaybeModelID(Maybe<ModelID> value) => new MaybeModelID(value.IsValuePresent, value.IsValuePresent ? value.Value : default);
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