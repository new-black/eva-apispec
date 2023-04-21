namespace EVA.SDK.Generator.V2.Exceptions;

public class ResponseParsingFailedException : SdkException
{
  public ResponseParsingFailedException(string url) : base($"Got an unexpected response from {url}")
  {
  }
}