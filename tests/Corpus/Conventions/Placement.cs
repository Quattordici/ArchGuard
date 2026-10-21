// mustBeIn and mustNotBeIn, with layers and namespace patterns. The message says where the type is. Layer rules still apply alongside.
using App.Application;

namespace App.Domain.Orders
{
    public interface IOrderRepository { }

    public class OrderRepository { } // a class: the convention selects interfaces only

    public sealed class CreateOrderHandler : IRequestHandler<string, int> { } // ARCH102 T:App.Domain.Orders.CreateOrderHandler mustBeIn=Application "CreateOrderHandler" ARCH001 T:App.Domain.Orders.CreateOrderHandler T:App.Application.IRequestHandler`2 "IRequestHandler"
}

namespace App.Infrastructure.Persistence
{
    internal interface ICustomerRepository { } // ARCH102 T:App.Infrastructure.Persistence.ICustomerRepository mustBeIn=Domain "ICustomerRepository"
}

namespace App.Shared
{
    public interface IClockRepository { } // ARCH102 T:App.Shared.IClockRepository mustBeIn=Domain "IClockRepository"
}

namespace App.Api.Old
{
    [System.Obsolete]
    internal sealed class OldEndpoint { } // ARCH102 T:App.Api.Old.OldEndpoint mustNotBeIn=App.Api.** "OldEndpoint"
}

namespace App.Application.Old
{
    [System.Obsolete]
    public sealed class OldService { }
}

namespace App.Domain.Legacy
{
    public sealed class LegacyHandler : IRequestHandler<int, int> { } // excluded: not checked
}
