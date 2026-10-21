# ARCH002: Forbidden namespace used

Code in a layer uses a type from a namespace that the layer's `mustNotDependOn` names, for example Entity Framework in `Domain`.

```
Domain must not depend on 'Microsoft.EntityFrameworkCore': 'Order.Save' uses 'DbContext'. Forbidden by mustNotDependOn in archie.json.
```

**Fix it** by moving the code to a layer that may use the namespace. If a layer dependency is broken too, only ARCH001 is reported.

Default severity: error. Properties as for ARCH001; `to` is empty unless the namespace is also in a layer.
