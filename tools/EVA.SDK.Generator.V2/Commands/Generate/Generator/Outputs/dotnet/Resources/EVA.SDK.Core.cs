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