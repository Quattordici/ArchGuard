# ARCH001: Layer dependency not allowed

Code in one layer uses a type from a layer it may not depend on.

```
Domain must not depend on Infrastructure: 'Order.Save' uses 'AppDbContext' (MyApp.Infrastructure.Persistence). Domain may depend on: nothing.
```

In the list form of `layers`, a layer may depend on the layers after it. In the named form, on the layers in its `mayDependOn`.

**Fix it** by moving the code, or by depending on an abstraction in an allowed layer (an interface in `Domain` implemented in
`Infrastructure`). If the dependency is intended, allow it in `archie.json`. For legacy code, `exclude` its namespace.

Default severity: error. The diagnostic's properties carry `from`, `to`, `target` (namespace), `sourceId` and `targetId` (doc-comment IDs).
