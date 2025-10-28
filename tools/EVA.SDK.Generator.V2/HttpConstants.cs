namespace EVA.SDK.Generator.V2;

internal static class HttpConstants
{
  internal const string UserAgent = "eva-sdk-generator/1.0.0";
  internal const string RefsUrl = "https://api.github.com/repos/new-black/eva-apispec/git/refs/tags";
  internal const string LatestSpecUrl = "https://raw.githubusercontent.com/new-black/eva-apispec/main/spec/spec.json";
  internal const string SpecificVersionUrl = "https://raw.githubusercontent.com/new-black/eva-apispec/spec/{0}/spec/spec.json";
  internal const string EvaEndpoint = "/api/definition.json";
  internal const string LatestReleaseTage = "https://api.github.com/repos/new-black/eva-apispec/releases/latest";
  internal const string TagPrefix = "tools/";
}