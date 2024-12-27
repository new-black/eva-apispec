using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EVA.SDK.Core;
using Newtonsoft.Json;

namespace EVA.SDK;

public class EVAApiClient : EVAApiClient<object, ServiceOptions>
{
    /// <summary>
    /// Creates a new EVAClient which allows you to easily call EVA services based on an anonymous user.
    /// </summary>
    /// <param name="baseUrl">The URL to your EVA installation.</param>
    /// <param name="userAgent">A string identifying the application making the request. Must consist of an alphanumeric name followed by an optional version number, separated by a forward slash. For example: EVA-SDK and EVA-SDK/1.0.0.</param>
    /// <param name="configuration">Object to pass in advanced configuration options</param>
    public EVAApiClient(Uri baseUrl, string userAgent, EVAClientConfiguration configuration) : base(baseUrl, null, userAgent, configuration)
    {
    }

    /// <summary>
    /// Creates a new EVAClient which allows you to easily call EVA services.
    /// </summary>
    /// <param name="baseUrl">The URL to your EVA installation.</param>
    /// <param name="apiKey">The API key that is used to authenticate requests made with this client. When not provided, service calls will be done with the anonymous user.</param>
    /// <param name="userAgent">A string identifying the application making the request. Must consist of an alphanumeric name followed by an optional version number, separated by a forward slash. For example: EVA-SDK and EVA-SDK/1.0.0.</param>
    /// <param name="configuration">Object to pass in advanced configuration options</param>
    public EVAApiClient(Uri baseUrl, string? apiKey, string userAgent, EVAClientConfiguration configuration) : base(baseUrl, apiKey, userAgent, configuration)
    {
    }
}

public partial class EVAApiClient<TState, TServiceCallOptions> : IEVAApiClient<TServiceCallOptions> where TServiceCallOptions : ServiceOptions where TState : class, new()
{
    public Uri BaseUrl { get; }

    private readonly string? _apiKey;
    private readonly string _userAgent;
    private readonly EVAClientConfiguration _configuration;
    private const string _targetedVersion = "<<API_VERSION>>";
    private readonly Encoding _encoding = new UTF8Encoding(false);

    private readonly JsonSerializer _newtonsoftJsonSerializer = new JsonSerializer
    {
      DateFormatHandling = DateFormatHandling.IsoDateFormat,
      DateTimeZoneHandling = DateTimeZoneHandling.Utc,
      NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>
    /// Creates a new EVAClient which allows you to easily call EVA services.
    /// </summary>
    /// <param name="baseUrl">The URL to your EVA installation.</param>
    /// <param name="apiKey">The API key that is used to authenticate requests made with this client. When not provided, service calls will be done with the anonymous user.</param>
    /// <param name="userAgent">A string identifying the application making the request. Must consist of an alphanumeric name followed by an optional version number, separated by a forward slash. For example: EVA-SDK and EVA-SDK/1.0.0.</param>
    /// <param name="configuration">Object to pass in advanced configuration options</param>
    public EVAApiClient(Uri baseUrl, string? apiKey, string userAgent, EVAClientConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(userAgent);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.WarningCallback);

        BaseUrl = baseUrl;
        _apiKey = apiKey;
        _userAgent = userAgent;
        _configuration = configuration;

        if (!UserAgentRegex().IsMatch(_userAgent))
        {
            throw new ArgumentException("UserAgent not in correct format, for example: MyCompany-OMS or MyCompany-OMS/1.34.3");
        }

        Setup();
    }

    partial void Setup();

    public virtual async Task<TResponse> CallService<TResponse>(IResponseType<TResponse> requestMessage, TServiceCallOptions? options = default)
      where TResponse : class, IResponseMessage
    {
        ArgumentNullException.ThrowIfNull(requestMessage);

        HttpClient? httpClient = null;

        try
        {
            httpClient = await _configuration.HttpClientFactory();
            var path = requestMessage.Path;

            var url = new Uri(BaseUrl, path);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrEmpty(_apiKey))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("eva", _apiKey);
            }

            httpRequest.Headers.Add("EVA-User-Agent", _userAgent);
            httpRequest.Headers.Add("EVA-API-Version", _targetedVersion);

            if (options?.IDsMode is EVAClientIDsMode.ExternalIDs or EVAClientIDsMode.Hybrid)
            {
              httpRequest.Headers.Add("EVA-IDs-Mode", options.IDsMode.ToString());
            }
            else
            {
              httpRequest.Headers.Add("EVA-IDs-Mode", "StringIDs");
            }

            if (options?.ExternalIDsLeniencyThreshold.HasValue ?? false)
            {
              httpRequest.Headers.Add("EVA-IDs-LeniencyThreshold", options.ExternalIDsLeniencyThreshold.Value.ToString());
            }

            if (!string.IsNullOrWhiteSpace(options?.OrganizationUnitID))
            {
              httpRequest.Headers.Add("EVA-Requested-OrganizationUnitID", options.OrganizationUnitID);
            }
            else if (options?.OrganizationUnitQuery != null)
            {
              var a = options.OrganizationUnitQuery.Name?.Replace("\"", "\\\"");
              var b = options.OrganizationUnitQuery.BackendID?.Replace("\"", "\\\"");
              var value = $"{{\"Name\":\"{a}\",\"BackendID\":\"{b}\"}}";

              httpRequest.Headers.Add("EVA-Requested-OrganizationUnit-Query", value);
            }

            if (options?.Culture != null)
            {
              httpRequest.Headers.Add("Accept-Language", options.Culture);
            }

            if (!string.IsNullOrWhiteSpace(options?.StationID))
            {
              httpRequest.Headers.Add("EVA-StationID", $"{options.StationID}");
            }

            // Build the content
            var ms = new MemoryStream();
            await using (var sw = new StreamWriter(ms, _encoding, leaveOpen: true))
            {
              _newtonsoftJsonSerializer.Serialize(sw, requestMessage);
            }

            ms.Position = 0;
            using var httpContent = new StreamContent(ms);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json", "utf-8");
            httpRequest.Content = httpContent;

            TState? state = new TState();
            await OnBeforeRequest(httpClient, requestMessage, httpRequest, options, state);

            using var response = await httpClient.SendAsync(httpRequest);

            // Handle the warning headers
            if (response.Headers.TryGetValues("EVA-Warning", out var warnings))
            {
                foreach (var warning in warnings)
                {
                    await _configuration.WarningCallback(path, warning);
                }
            }

            // Deserialize the response
            TResponse responseMessage = default;
            if (response.Content.Headers.ContentLength == 0)
            {
              // No-op we won't try to deserialize an empty response
            }
            else
            {
              await using var responseStream = await response.Content.ReadAsStreamAsync();

              using var sr = new StreamReader(responseStream);
              using var jsonTextReader = new JsonTextReader(sr);

              responseMessage = _newtonsoftJsonSerializer.Deserialize<TResponse>(jsonTextReader);
            }

            await OnAfterRequest(httpClient, httpRequest, response, responseMessage, options, state);

            // Handle error status
            if (!response.IsSuccessStatusCode)
            {
                throw new EVAApiCallException(response.StatusCode, response.Headers, responseMessage?.Error, responseMessage);
            }

            return responseMessage;
        }
        finally
        {
            if (httpClient != null && _configuration.DisposeHttpClient)
            {
                httpClient.Dispose();
            }
        }
    }

    protected virtual ValueTask OnBeforeRequest(HttpClient client, object requestMessage, HttpRequestMessage request, TServiceCallOptions? options, TState state)
    {
      return ValueTask.CompletedTask;
    }

    protected virtual ValueTask OnAfterRequest(HttpClient client, HttpRequestMessage request, HttpResponseMessage response, IResponseMessage? responseMessage, TServiceCallOptions? options, TState state)
    {
      return ValueTask.CompletedTask;
    }

    [GeneratedRegex(@"(^[\w\-]{3,50})(/(?:[\w\.\-]*)+)?")]
    private static partial Regex UserAgentRegex();
}

public class EVAClientConfiguration
{
    /// <summary>
    /// This callback is triggered when a call to EVA returns warnings.
    /// The first argument is the path of the request, the second is the warning message.
    /// </summary>
    public required Func<string, string, ValueTask> WarningCallback { get; set; }

    /// <summary>
    /// Factory to provide an HttpClient for the EVAClient to use.
    /// </summary>
    public required Func<ValueTask<HttpClient>> HttpClientFactory { get; set; }

    /// <summary>
    /// Whether the HttpClient should be disposed when the EVAClient is disposed.
    /// </summary>
    public required bool DisposeHttpClient { get; set; }
}

public class EVAApiCallException : Exception
{
    public EVAApiCallException(HttpStatusCode statusCode, HttpResponseHeaders headers, ServiceError? error, object responseMessage)
    {
        StatusCode = statusCode;
        ResponseHeaders = headers;
        Error = error;
        ResponseMessage = responseMessage;
    }

    public HttpResponseHeaders ResponseHeaders { get; }
    public HttpStatusCode StatusCode { get; }
    public ServiceError? Error { get; set; }
    public object? ResponseMessage { get; set; }
}

public class ServiceOptions
{
    /// <summary>
    /// Perform the request in the context of the specified OrganizationUnitID
    /// </summary>
    public string? OrganizationUnitID { get; set; }

    /// <summary>
    /// Perform the request in the context of the OrganizationUnit that matches the specified query.
    /// </summary>
    public RequestedOrganizationUnitQuery? OrganizationUnitQuery { get; set; }

    /// <summary>
    /// Pass in a full culture for this request. Eg: nl-NL or en-US
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Perform the request in the context of the specified StationID
    /// </summary>
    public string? StationID { get; set; }

    public int? ExternalIDsLeniencyThreshold { get; set; }

    public EVAClientIDsMode IDsMode { get; set; }
}

public enum EVAClientIDsMode
{
  Default = 0,
  Hybrid = 1,
  ExternalIDs = 2
}

public class RequestedOrganizationUnitQuery
{
    public string? BackendID { get; set; }
    public string? Name { get; set; }
}