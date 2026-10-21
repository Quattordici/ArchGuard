# ARCH105: Type's kind or modifiers break a convention

A type that a convention applies to is not everything `mustBe` lists, or is something `mustNotBe` lists. The words are the C# ones:
`public`, `internal`, `abstract`, `sealed`, `static`, `record`, `class`, `interface`, `struct`, `enum`, `delegate`. `sealed`, `abstract`
and `static` are about classes (a struct is not "sealed"); `record` covers record classes and record structs; `public`/`internal` are the
declared accessibility.

```jsonc
"conventions": [
  { "name": "DomainEvents", "types": { "inherit": "IDomainEvent", "areNot": "interface" }, "mustBe": ["sealed", "record"] },
  { "types": { "in": "Infrastructure", "are": "class" }, "mustNotBe": "public" }
]
```

```
'OrderShipped' must be sealed and record (it is not sealed). Convention 'DomainEvents' in archie.json applies to types that are not interface and inherit 'IDomainEvent'.
```

**Fix it** by changing the declaration, or narrow the convention's `types`.

Default severity: error. Properties as for [ARCH101](ARCH101.md).
