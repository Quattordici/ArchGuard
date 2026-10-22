namespace Shop.Domain.Orders;

public interface IOrderRepository
{
    Order? Find(Guid id);
}

public interface IHandler<TRequest, TResult>
{
    TResult Handle(TRequest request);
}
