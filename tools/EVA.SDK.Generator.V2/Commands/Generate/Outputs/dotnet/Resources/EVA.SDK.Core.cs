namespace EVA.SDK
{
  public interface IEVAApiClient<TOptions>
  {
    public System.Threading.Tasks.Task<TResponse> CallService<TResponse>(EVA.SDK.Core.IResponseType<TResponse> requestMessage, TOptions? options = default)
      where TResponse : class, EVA.SDK.Core.IResponseMessage;
  }
}

namespace EVA.SDK.Core
{
  public interface IResponseMessage
  {
    ServiceError? Error { get; }
    ResponseMessageMetadata? Metadata { get; }
  }

  public interface IResponseType<TResponse> where TResponse : class, IResponseMessage
  {
    string Path { get; }
  }

  public class PageConfig : EVA.SDK.Core.PageConfig<IDictionary<string?, string?>?>
  {

  }

  public struct Maybe<T>
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

    public override string? ToString()
    {
      if (!IsValuePresent) return "{no value}";
      if (Value == null) return "{null}";
      return Value.ToString();
    }
  }
}