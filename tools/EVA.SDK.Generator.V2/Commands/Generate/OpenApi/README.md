# Generating from the OpenAPI document

With `--token`, `generate <output>` downloads the OpenAPI document of an EVA environment (`<--in>/openapi.json`, gzip-aware, the token
must belong to a user with the `ViewApiSpecification` functionality) and generates the output from that document. Without `--token` the
specification pipeline (`spec.json`) runs as before. The document format is described in `EVA.OpenAPI.Generator/README.md` in the EVA
repository.

```shell
EVA.SDK.Generator.V2 generate typescript -i https://api.euw.acme.test.eva-online.cloud --token <token> -o ./out
```

IDs are always strings. `--use-string-ids` has no effect, `--api` only supports `eva`, and the `evaspec` and `openapi` outputs are not
available: the document itself is the specification.

## Structure

- `ApiInput` downloads and loads the document with Microsoft.OpenApi. The hidden `--openapi` flag reads the document from the file given
  as `--in` instead, for local EVA builds such as the EVA test suite. SDKs are generated from an EVA environment with `--token`.
- `Model/` holds `EvaApi`, the API described by the document. Its services, types, properties and errors are views on the OpenAPI objects:
  they are computed from the document when read, and transforms change the document itself and call `EvaApi.Invalidate()`.
  - `EvaApi.Types` are the types outputs generate. Constructed generic types are left out while they refer to their generic type (`x-eva-template`),
    a reference to them is a `NamedRef` to the generic type with its arguments. After `remove-generics` they are regular types.
  - `EvaApi.AllTypes` are all component schemas, for transforms.
  - `TypeRef` is the type of a property, base type, generic argument or error argument: `PrimitiveRef`, `ArrayRef`, `MapRef`, `NamedRef`,
    `TypeParameterRef` or `OptionRef`, each with `Nullable`.
  - `Schemas` reads, walks, replaces and copies schemas, including the schemas held by EVA extensions. Replacing a schema keeps the keywords
    that describe the property it belongs to.
- `Transforms/` holds the transforms (same IDs as the specification pipeline) and the `--assembly`/`--service` filters.
- `ApiGenerationPipeline` runs filters, transforms (until nothing changes) and the output.
- `Outputs/<output>` holds the outputs.

## Differences with the specification pipeline

The outputs are generated the same way. On the same EVA build, the remaining differences are:

- Event exports and data lake models are not part of the document, nor are the types only they use.
- The document has the errors of every assembly it exposes types of, a superset. As before, errors don't keep the types of their arguments
  in the output, only their names in the comments.
- Constructed generic types are expanded from the actual types, so a type parameter filled in with a value type (eg an enum or `bool`)
  is not nullable, and the names of flattened types say so (`NotNull...`). IDs in generic arguments of base types are strings as well.
- Errors are listed by name, services by ordinal name.
- Whether a type is used in requests and/or responses is determined from the document after the transforms and filters, not before them.
- `remove-generics` turns a generic type whose arguments don't matter (eg `RequestMessage<TResponse>`) into one type, instead of a copy per
  argument.
