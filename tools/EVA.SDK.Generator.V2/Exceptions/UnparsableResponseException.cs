namespace EVA.SDK.Generator.V2.Exceptions;

public class UnparsableResponseException : SdkException
{
  public UnparsableResponseException(string url) : base($"Got an unexpected response from {url}")
  {
  }
}