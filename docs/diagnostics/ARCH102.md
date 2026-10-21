# ARCH102: Type is in the wrong place

A type that a convention applies to is not in a layer or namespace of `mustBeIn`, or is in one of `mustNotBeIn`.

```jsonc
"conventions": [
  { "name": "Repositories", "types": { "named": "I*Repository", "are": "interface" }, "mustBeIn": "Domain" },
  { "types": { "haveAttribute": "Obsolete" }, "mustNotBeIn": "MyApp.Api.**" }
]
```

```
'ICustomerRepository' must be in Domain, but is in Infrastructure (MyApp.Infrastructure.Persistence). Convention 'Repositories' in archie.json applies to types named 'I*Repository' that are interface.
```

A bare name is a layer; anything with a dot or a wildcard is a namespace (write `Shared.**` for a one-segment namespace). A type's place
is its namespace, like for the layer rules.

**Fix it** by moving the type (namespace), or narrow the convention's `types`.

Default severity: error. Properties as for [ARCH101](ARCH101.md).
