using Shop.Infrastructure.Persistence;

namespace Shop.Domain.Orders;

public sealed class Order(Guid id)
{
    public Guid Id { get; } = id;

    public List<OrderLine> Lines { get; } = [];

    // ARCH001: Domain must not depend on Infrastructure.
    public void Save(AppDbContext db) => db.Save(this);

    // ARCH002: System.Net.Http is in Domain's mustNotDependOn.
    public Task<string> FetchPriceAsync(HttpClient client) => client.GetStringAsync("https://prices.example/" + Id);
}

public sealed record OrderLine(string Sku, int Quantity);
