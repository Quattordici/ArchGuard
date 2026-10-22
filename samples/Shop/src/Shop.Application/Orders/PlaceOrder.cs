using Shop.Domain.Orders;

namespace Shop.Application.Orders;

public sealed record PlaceOrderCommand(Guid OrderId);

// Follows the conventions: named *Handler, in Application.
public sealed class PlaceOrderHandler(IOrderRepository orders) : IHandler<PlaceOrderCommand, bool>
{
    public bool Handle(PlaceOrderCommand request) => orders.Find(request.OrderId) is not null;
}

// ARCH101: a handler that is not named *Handler.
public sealed class CancelOrderProcessor(IOrderRepository orders) : IHandler<Guid, bool>
{
    public bool Handle(Guid request) => orders.Find(request) is not null;
}

// ARCH102: a repository outside Domain.
public interface ICustomerRepository
{
    object? Find(Guid id);
}
