# New Black EVA SDK

This package provides a C# SDK for the New Black EVA API.

## Installation

```bash
dotnet add package NewBlack.EVA.SDK
```

This package includes all API types which is likely more than what you actually need.
If you need to reduce the size of your application, this SDK supports the use of trimming.

## Breaking changes compared to 2.0

- `EVAClient` has been renamed to `EVAApiClient`
- `EVAServiceCallException` has been renamed to `EVAApiCallException
- `EVAApiClient` now requires a configuration object
- Client is no longer disposable

## Usage

```csharp
var config = new EVAClientConfiguration
{
  WarningCallback = async (path, warning) =>
  {
    // Sample implementation, make sure to log this somewhere accessible
    Console.WriteLine("[{0}]: {1}", path, warning);
  },

  // This is called for every request and should ideally use an IHttpClientFactory
  HttpClientFactory = async () => new System.Net.Http.HttpClient(),
  DisposeHttpClient = true
};

var client = new EVAApiClient(
  new Uri("https://my.api.endpoint"),
  "my_api_key",
  "MyUserAgent/1.0.0",
  config
);

var response = await client.GetUser(new GetUser
{
  ID = "1234"
});
```

## Dynamic data

The API has accepts different models on the same property in some places. This is handled through `DynamicData<>` types that specify the shared contract.

Use it in requests:

```csharp
var response = await client.CreateDevice(new CreateDevice
{
  // Data is of type DynamicData<IDeviceTypeData>
  Data = new ThermalPrinterDeviceTypeData
  {
    HasCashDrawer = true
  }
});
```

And in responses:

```csharp
var response = await client.GetDevice(new GetDevice
{
  ID = "1234"
});

// response.Data is of type DynamicData<IDeviceTypeData>
var data = response.Data.ToThermalPrinterDeviceTypeData();
```