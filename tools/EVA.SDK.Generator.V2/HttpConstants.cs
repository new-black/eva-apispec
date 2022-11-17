namespace EVA.SDK.Generator.V2;

public static class HttpConstants
{
  public static string UserAgent = "eva-sdk-generator/1.0.0";
  public static string RefsUrl = "https://api.github.com/repos/new-black/eva-apispec/git/refs/tags";
  public static string LatestSpecUrl = "https://raw.githubusercontent.com/new-black/eva-apispec/main/spec/spec.json";
  public static string SpecificVersionUrl = "https://raw.githubusercontent.com/new-black/eva-apispec/spec/{0}/spec/spec.json";
  public static string EvaEndpoint = "/api/core/GetApiDefinition";
  public static string LatestReleaseTage = "https://api.github.com/repos/new-black/eva-apispec/releases/latest";
  public static string TagPrefix = "tools/";
}