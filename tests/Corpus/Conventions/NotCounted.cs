// Code that no convention applies to, or that meets them all: no diagnostics.
namespace App.Application.Customers
{
    public sealed class GetCustomerHandler : App.Application.IRequestHandler<int, string> { }

    public interface IGetCustomer { }

    public enum CustomerKind { Person, Company }

    public delegate void CustomerChanged(int id);
}

namespace App.Domain.Customers
{
    public interface ICustomerRepository { }
}

// Top-level code outside the layers that no convention selects.
public sealed class GlobalThing { }
