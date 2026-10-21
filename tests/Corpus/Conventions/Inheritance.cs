// mustInherit and mustNotInherit look through the whole base class chain; selectors by a full name match only that namespace.
namespace App.Domain.Orders
{
    public class OrderContext : Vendor.Orm.DbContext { } // ARCH103 T:App.Domain.Orders.OrderContext mustNotInherit=Vendor.Orm.DbContext "OrderContext"

    public class Indirect : OrderContext { } // ARCH103 T:App.Domain.Orders.Indirect mustNotInherit=Vendor.Orm.DbContext "Indirect"

    public class DbContext { }

    public class SameName : DbContext { } // a DbContext in another namespace
}

namespace App.Api.Orders
{
    [Vendor.Web.ApiController]
    public sealed class OrdersController : Vendor.Web.ControllerBase { }

    [Vendor.Web.ApiController]
    public sealed class PlainController { } // ARCH103 T:App.Api.Orders.PlainController mustInherit=ControllerBase "PlainController"
}
