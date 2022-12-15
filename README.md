# EVA API

This repository contains the specification of the EVA API. Using the [EVA SDK Generator](https://github.com/new-black/eva-apispec/releases) you can generate typings to connect to EVA in multiple formats.

Use `./sdk-generator-win-x64.exe --help` to get an overview of all possible features.

If you find any bugs or you need a feature, please [create an issue](https://github.com/new-black/eva-apispec/issues) or contact us through your SL.

## Supported languages

We currently provide first-class support for:
- `dotnet`
- `typescript`
- `swift`
- `openapi`.

If your preferred language is not in this list, you can use the `openapi` target and use any of the available tools (https://github.com/OpenAPITools/openapi-generator) to generate an SDK.

## Examples

> All examples use the .exe, but tooling is also available for Linux and OSX.

Get the latest OpenAPI specification for EVA. 
```shell
./sdk-generator-win-x64.exe generate openapi -o ./out

// Output:
// ./out/openapi.json
```

Get the v2.0.670 OpenAPI YAML specification only for EVA.Core.Services.Management without inheritance across types.
```shell
./sdk-generator-win-x64.exe generate openapi -i v2.0.670 -o ./out --opt-format yaml --remove inheritance --assembly EVA.Core.Services.Management

// Output:
// ./out/openapi.json
```

Get the Swift typings from a given environment. Merge the types from assemblies without services and the management services into EVA.Core.

```shell
./sdk-generator-win-x64.exe generate swift -i https://my.eva-endpoint.eva-online.cloud -o ./out --remove unused-types --orphaned-types-assembly EVA.Core --assembly EVA.Core.Services.Management:EVA.Core EVA.Core

// Output:
// ./out/Core/GetUser.swift
// ...etc...
```