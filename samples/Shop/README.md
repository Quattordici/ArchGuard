# Shop: Archie on a small layered solution

Three layers and a test project, with deliberate violations. Archie is added once in `Directory.Build.props`, and the rules are in
`archie.json` next to `Shop.slnx`. No project file mentions Archie.

```bash
dotnet build samples/Shop/Shop.slnx
```

The build fails. Archie reports each use of a forbidden type, so one line can give two errors:

| File | ID | Why |
|---|---|---|
| `Shop.Domain/Orders/Order.cs` (12,22) and (12,45) | ARCH001 | `Order.Save` takes an `AppDbContext` and calls `db.Save`; Domain may depend on nothing |
| `Shop.Domain/Orders/Order.cs` (15,41) and (15,70) | ARCH002 | `Order.FetchPriceAsync` takes an `HttpClient` and calls `GetStringAsync`; `System.Net.Http` is in Domain's `mustNotDependOn` |
| `Shop.Application/Orders/PlaceOrder.cs` (14,21) | ARCH101 | `CancelOrderProcessor` handles a request but is not named `*Handler` |
| `Shop.Application/Orders/PlaceOrder.cs` (20,18) | ARCH102 | `ICustomerRepository` is a repository outside Domain |

`Shop.Application` references `Shop.Domain`, so MSBuild only compiles it, and shows ARCH101 and ARCH102, once Domain builds. To see all
four at once: `dotnet build samples/Shop/Shop.slnx -p:NoWarn=ARCH001%3BARCH002` shows the Application errors (and proves that
`NoWarn` works for Archie like for any analyzer).

Not reported: `PlaceOrderHandler` (follows the conventions) and `OrderTests` (test projects are not checked, even though its namespace
is in the Domain layer and it uses Infrastructure). `Shop.Domain` references `Shop.Infrastructure`, which is the mistake Archie catches:
the compiler can't, because the reference makes the code valid.

Try: fix one violation and rebuild; change a severity in `.editorconfig` (`dotnet_diagnostic.ARCH101.severity = warning`); add a typo
to `archie.json` (`"mayDependsOn"`) and see ARCH901 at that line.
