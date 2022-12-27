namespace EVA.SDK.Generator.V2.Exceptions;

public class NonSuccessStatusCodeException : SdkException
{
  public NonSuccessStatusCodeException(HttpResponseMessage msg) : base($"Got unexpected statuscode ({msg.StatusCode}) from {msg.RequestMessage?.RequestUri}")
  {

  }
}