# Conformance corpus

Each case in `Dependencies/` (layer rules) and `Conventions/` (conventions) is C# source plus the violations it must produce, written as trailing comments on the line where the
squiggle goes:

```csharp
private Db _db = null!; // ARCH001 F:App.Domain.Cases.Members._db T:App.Infrastructure.Db "Db"
```

That is the diagnostic ID, the doc-comment ID of the source member (`sourceId`), the doc-comment ID of the used type (`targetId`) and the
text the diagnostic is reported on. A line may carry several. A case without expectations must produce nothing.

For conventions, `sourceId` is the type and `targetId` the requirement as written (`mustBeNamed=*Handler`):

```csharp
public sealed class PlaceOrderProcessor : IRequestHandler<string, int> { } // ARCH101 T:App.Application.Orders.PlaceOrderProcessor mustBeNamed=*Handler "PlaceOrderProcessor"
```

- `archie.json` in each folder is the config of every case in it.
- Files starting with `_` are compiled into every case (the types the cases depend on) and must produce nothing themselves.

`tests/Archie.Analyzers.Tests/CorpusTests.cs` runs every case through the analyzer, on the newest Roslyn and on 4.8. The
`(ID, sourceId, targetId)` part is the violation key that the Phase 3 baseline and the Phase 4 metadata backend share (plan §14.2), so the
metadata backend will run the same cases; line and text are analyzer-only.
