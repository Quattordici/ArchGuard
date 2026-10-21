# ARCH101: Type name breaks a convention

A type that a convention in archie.json applies to is not named as `mustBeNamed` says, or is named as `mustNotBeNamed` forbids.

```jsonc
"conventions": [
  { "name": "Handlers", "types": { "inherit": "IRequestHandler<,>" }, "mustBeNamed": "*Handler" },
  { "mustNotBeNamed": ["*Manager", "*Helper"] }
]
```

```
'PlaceOrderProcessor' must be named '*Handler'. Convention 'Handlers' in archie.json applies to types that inherit 'IRequestHandler<,>'.
```

Names are matched without namespace and type parameters (`Repository<T>` is `Repository`); `*` is any characters, case-sensitive.

**Fix it** by renaming the type, or narrow the convention's `types` if it should not apply.

Default severity: error. Properties: `convention` (its `name`, or `conventions[i]`), `sourceId` (doc-comment ID of the type), `targetId`
(the requirement as written, e.g. `mustBeNamed=*Handler`) and `kind` (the key). Reported once per type, on the name of its first declaration.
