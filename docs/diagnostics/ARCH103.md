# ARCH103: Type's base types break a convention

A type that a convention applies to does not inherit everything `mustInherit` names, or inherits something `mustNotInherit` names.
`inherit` covers base classes at any depth and interfaces, implemented directly or through a base type or another interface.

```jsonc
"conventions": [
  { "name": "Controllers", "types": { "in": "Api", "named": "*Controller" }, "mustInherit": "ControllerBase" },
  { "types": { "in": "Domain" }, "mustNotInherit": "Microsoft.EntityFrameworkCore.DbContext" }
]
```

```
'OrderContext' must not inherit 'Microsoft.EntityFrameworkCore.DbContext'. conventions[1] in archie.json applies to types in Domain.
```

A type name without namespace matches that name in any namespace; with one, only there. `<,>` fixes the number of type parameters
(`IRequestHandler<,>` or ``IRequestHandler`2``), none means any.

**Fix it** by changing the base type, or narrow the convention's `types`.

Default severity: error. Properties as for [ARCH101](ARCH101.md).
