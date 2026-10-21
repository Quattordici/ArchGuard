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

## Severity and suppression

| ID | Meaning | Default |
|---|---|---|
| [ARCH001](docs/diagnostics/ARCH001.md) | Layer dependency not allowed | error |
| [ARCH002](docs/diagnostics/ARCH002.md) | Forbidden namespace used | error |
| [ARCH900](docs/diagnostics/ARCH900.md) | archie.json could not be read | error |
| [ARCH901](docs/diagnostics/ARCH901.md) | Unknown or unsupported key in archie.json | error |
| [ARCH902](docs/diagnostics/ARCH902.md) | Invalid value in archie.json | error |
| [ARCH909](docs/diagnostics/ARCH909.md) | No archie.json found | info |

Severity is set the standard way, not in archie.json: `dotnet_diagnostic.ARCH001.severity = warning` in `.editorconfig` (ARCH909 has no
source location, so it needs a global analyzer config). Suppress one place with `#pragma warning disable ARCH001` or
`[SuppressMessage("Archie", "ARCH001", Justification = "...")]`.

## Enforcing in CI

With the package referenced and `archie.json` present, `dotnet build` fails on a violation, so CI does too, with no extra setup.

What this version does **not** guarantee: that nobody switched the rules off. `#pragma`, `SuppressMessage`, `NoWarn`, an `.editorconfig`
severity, `RunAnalyzers=false` and `ArchieEnabled=false` all silence Archie, and a reviewer may not notice. Put `archie.json` under
CODEOWNERS, since it is the one file that legitimately loosens the rules. A `dotnet archie verify` step that fails CI when any of those is used
is planned.

## Building

See [CONTRIBUTING.md](CONTRIBUTING.md).
