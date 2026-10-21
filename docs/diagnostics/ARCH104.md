# ARCH104: Type's attributes break a convention

A type that a convention applies to is not declared with every attribute `mustHaveAttribute` names, or has one `mustNotHaveAttribute`
names. Only the type's own attributes count; an attribute whose class derives from the one named counts too. `Attribute` may be left off,
like in C#.

```jsonc
"conventions": [
  { "name": "Controllers", "types": { "in": "Api", "named": "*Controller" }, "mustHaveAttribute": "ApiController" }
]
```

```
'CustomersController' must have attribute 'ApiController'. Convention 'Controllers' in archie.json applies to types in Api named '*Controller'.
```

**Fix it** by adding or removing the attribute, or narrow the convention's `types`.

Default severity: error. Properties as for [ARCH101](ARCH101.md).
