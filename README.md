# Archie
Architecture rules for .NET, enforced at compile time. Roslyn analyzers turn layering and dependency violations into build errors and IDE diagnostics with exact file and line.

> **Status: early development.** `0.0.1` on NuGet only reports ARCH909 (no `archie.json` found). The layer rules below are in the next release.

## Getting started

```xml
<!-- Directory.Build.props -->
<ItemGroup>
  <PackageReference Include="Archie" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

```jsonc
// archie.json, next to the .sln
{
  "$schema": "https://raw.githubusercontent.com/Quattordici/Archie/main/schema/v1.json",
  "rootNamespace": "MyApp",
  "layers": ["Api", "Infrastructure", "Application", "Domain"]
}
```

Read the list top to bottom: **each layer may depend on the layers below it.** `Domain` depends on nothing, `Api` may depend on everything.
Layer `Domain` is the namespace `MyApp.Domain` and everything under it. That is the whole config for a conventional clean, onion or n-tier
solution. In the IDE and in `dotnet build` you then see:

```
src/MyApp.Domain/Orders/Order.cs(14,17): error ARCH001: Domain must not depend on Infrastructure:
  'Order.Save' uses 'AppDbContext' (MyApp.Infrastructure.Persistence). Domain may depend on: nothing.
```

## More rules

**Any other graph:** name the layers and say what each one may depend on. A layer without `mayDependOn` may depend on nothing but itself.

```jsonc
{
  "layers": {
    "Domain":         "MyApp.Domain",
    "Application":    { "namespaces": "MyApp.Application",    "mayDependOn": ["Domain"] },
    "Infrastructure": { "namespaces": "MyApp.Infrastructure", "mayDependOn": ["Application", "Domain"] },
    "Api":            { "namespaces": "MyApp.Api",            "mayDependOn": ["Application", "Infrastructure"] }
  }
}
```

**Namespaces that don't match the layer name:** in the list, write the layer as an object. `*` is one namespace segment, `**` any number.

```jsonc
"layers": [
  "Web",
  { "name": "Infrastructure", "namespaces": ["Contoso.Shop.Infrastructure", "Contoso.Shop.Persistence"] },
  "Application",
  { "name": "Domain", "namespaces": ["Contoso.Shop.Domain", "Contoso.SharedKernel"] }
]
```

**Outside namespaces:** code outside the layers (the BCL, NuGet packages, `MyApp.Shared`) may be used by any layer unless `mustNotDependOn`
names it (ARCH002):

```jsonc
"mustNotDependOn": {
  "Domain": ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "System.Net.Http"]
}
```

**Legacy code:** `"exclude": ["MyApp.Domain.Legacy"]` stops checking code in those namespaces. Code that depends *on* them is still checked.

**Not checked:** test projects (`IsTestProject`, set by `Microsoft.NET.Test.Sdk`; turn them back on with `"checkTestProjects": true`), generated
code, and any project with `<ArchieEnabled>false</ArchieEnabled>`.

**Which archie.json:** the nearest one above the project (`GetPathOfFileAbove`); put another closer to a project to give it its own. Set
`<ArchieConfig>` to choose a file explicitly.

### What counts as a dependency

Archie flags every place where code names or touches a type from a layer it may not depend on: base types and interfaces; field, property,
event, parameter, return and explicitly typed local variable types (through arrays, nullables, tuples and generic arguments); generic
constraints and written type arguments; attributes and their `typeof`/enum arguments; `new`, calls (an extension method counts as its static
class), member access, `typeof`, casts, `is`/`as`, patterns and `catch`.

Not counted: `using` directives, `nameof`, XML doc `cref`, types only inferred by `var`, and transitive dependencies (A uses B uses C). Each
place is its own diagnostic, reported on the type or member name.

## Conventions

Rules for the types themselves: how they are named, where they live, what they inherit, which attributes they carry, and their kind and
modifiers. Each convention says which `types` it applies to and what they must be:

```jsonc
"conventions": [
  { "name": "Handlers", "types": { "inherit": "IRequestHandler<,>" }, "mustBeNamed": "*Handler", "mustBeIn": "Application" },
  { "name": "Repositories", "types": { "named": "I*Repository", "are": "interface" }, "mustBeIn": "Domain" },
  { "name": "DomainEvents", "types": { "inherit": "IDomainEvent", "areNot": "interface" }, "mustBe": ["sealed", "record"] },
  { "types": { "in": "Domain" }, "mustNotInherit": "Microsoft.EntityFrameworkCore.DbContext" },
  { "types": { "in": "Infrastructure", "are": "class" }, "mustNotBe": "public" },
  { "mustNotBeNamed": ["*Manager", "*Helper"] }
]
```

```
src/MyApp.Application/Orders/PlaceOrder.cs(5,21): error ARCH101: 'PlaceOrderProcessor' must be named '*Handler'.
  Convention 'Handlers' in archie.json applies to types that inherit 'IRequestHandler<,>'.
```

| `types` (all keys must match) | Requirement (`mustNot…` forbids instead) | ID |
|---|---|---|
| `named`: name patterns, `*` is any characters | `mustBeNamed` (one of), `mustNotBeNamed` | ARCH101 |
| `in`: layers, or namespaces (with a dot or `*`) | `mustBeIn` (one of), `mustNotBeIn` | ARCH102 |
| `inherit`: base classes (any depth) and interfaces | `mustInherit` (all of), `mustNotInherit` | ARCH103 |
| `haveAttribute`: attributes on the type (`Attribute` may be left off) | `mustHaveAttribute` (all of), `mustNotHaveAttribute` | ARCH104 |
| `are` (all of), `areNot` (none of): `public` `internal` `abstract` `sealed` `static` `record` `class` `interface` `struct` `enum` `delegate` | `mustBe` (all of), `mustNotBe` | ARCH105 |

In `types`, a list means any of its values (`"named": ["*Handler", "*Processor"]`); without `types` a convention applies to every checked
type. Type names may leave out the namespace (then any namespace matches); `<,>` fixes the number of type parameters, none means any.
Excluded code, test projects and generated code are not checked, like for the layer rules. A config may have conventions and no layers.

## Severity and suppression

| ID | Meaning | Default |
|---|---|---|
| [ARCH001](docs/diagnostics/ARCH001.md) | Layer dependency not allowed | error |
| [ARCH002](docs/diagnostics/ARCH002.md) | Forbidden namespace used | error |
| [ARCH101](docs/diagnostics/ARCH101.md) | Type name breaks a convention | error |
| [ARCH102](docs/diagnostics/ARCH102.md) | Type is in the wrong place | error |
| [ARCH103](docs/diagnostics/ARCH103.md) | Type's base types break a convention | error |
| [ARCH104](docs/diagnostics/ARCH104.md) | Type's attributes break a convention | error |
| [ARCH105](docs/diagnostics/ARCH105.md) | Type's kind or modifiers break a convention | error |
| [ARCH900](docs/diagnostics/ARCH900.md) | archie.json could not be read | error |
| [ARCH901](docs/diagnostics/ARCH901.md) | Unknown or unsupported key in archie.json | error |
| [ARCH902](docs/diagnostics/ARCH902.md) | Invalid value in archie.json | error |
| [ARCH909](docs/diagnostics/ARCH909.md) | No archie.json found | info |

Severity is set the standard way, not in archie.json: `dotnet_diagnostic.ARCH001.severity = warning` in `.editorconfig` (ARCH909 has no
source location, so it needs a global analyzer config). Conventions have one ID per kind of requirement, so naming can be a warning while
placement stays an error. Suppress one place with `#pragma warning disable ARCH001` or
`[SuppressMessage("Archie", "ARCH001", Justification = "...")]`.

## Enforcing in CI

With the package referenced and `archie.json` present, `dotnet build` fails on a violation, so CI does too, with no extra setup.

What this version does **not** guarantee: that nobody switched the rules off. `#pragma`, `SuppressMessage`, `NoWarn`, an `.editorconfig`
severity, `RunAnalyzers=false` and `ArchieEnabled=false` all silence Archie, and a reviewer may not notice. Put `archie.json` under
CODEOWNERS, since it is the one file that legitimately loosens the rules. A `dotnet archie verify` step that fails CI when any of those is used
is planned.

## Building

See [CONTRIBUTING.md](CONTRIBUTING.md).
