# EVA API

This repository contains the specification of the EVA API. Using the [EVA SDK Generator](https://github.com/new-black/eva-apispec/releases) you can generate typings to connect to EVA in multiple formats.

Use `./sdk-generator-win-x64.exe --help` to get an overview of all possible features.

If you find any bugs or you need a feature, please [create an issue](https://github.com/new-black/eva-apispec/issues) or contact us through your SL. 

## Examples

> All examples use the .exe, but tooling is also available for Linux and OSX.

Get the latest OpenAPI specification for EVA. 
```shell
./sdk-generator-win-x64.exe generate openapi -o ./out

// Output:
// ./out/openapi.json
```

Get the latest OpenAPI YAML specification only for EVA.Core.Services.Management without inheritance.
```shell
./sdk-generator-win-x64.exe generate openapi -o ./out --opt-format yaml --remove inheritance unused-types --assembly EVA.Core.Services.Management

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