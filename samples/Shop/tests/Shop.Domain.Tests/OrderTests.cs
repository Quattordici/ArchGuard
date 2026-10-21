using Shop.Domain.Orders;
using Shop.Infrastructure.Persistence;

namespace Shop.Domain.Tests;

// The namespace matches the Domain layer, and the test uses Infrastructure: not reported, because test projects are not checked.
public sealed class OrderTests
{
    public void Saves() => new Order(Guid.NewGuid()).Save(new AppDbContext());
}
