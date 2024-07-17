public interface IResponseMessage
{
  public ServiceError? Error { get; }
  public ResponseMessageMetadata? Metadata { get; }
}

public interface IResponseType<out TResponse> where TResponse : IResponseMessage
{

}

public interface IEVAClient<TOptions>
{
  public System.Threading.Tasks.Task<TResponse> CallService<TResponse>(IResponseType<TResponse> requestMessage, TOptions options = default) where TResponse : IResponseMessage;
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

public interface IDynamicData
{
  JObject Data { get; set; }
}

public partial class DynamicData<TShared> : IDynamicData
{
  public TShared Shared => Data.ToObject<TShared>();

  public JObject Data { get; set; }

  public static implicit operator TShared(DynamicData<TShared> x)
  {
    return x.Shared;
  }

  public static implicit operator DynamicData<TShared>(TShared x)
  {
    return x == null ? null : new DynamicData<TShared> { Data = JObject.FromObject(x) };
  }
}

public static class DynamicDataExtensions {
  public static DynamicData<TShared> AsDynamic<TShared>(this TShared data) {
    return data == null ? null : new DynamicData<TShared> { Data = JObject.FromObject(data) };
  }
}