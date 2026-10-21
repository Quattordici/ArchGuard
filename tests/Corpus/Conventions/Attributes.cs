// mustHaveAttribute: the 'Attribute' suffix may be left off, and only the attribute's class (or a base class of it) counts.
namespace App.Api.Customers
{
    public sealed class CustomersController : Vendor.Web.ControllerBase { } // ARCH104 T:App.Api.Customers.CustomersController mustHaveAttribute=ApiController "CustomersController"

    [Vendor.Web.AreaApiController]
    public sealed class AreasController : Vendor.Web.ControllerBase { } // ARCH104 T:App.Api.Customers.AreasController mustHaveAttribute=ApiController "AreasController"

    [Vendor.Web.ApiControllerAttribute]
    public sealed class LongFormController : Vendor.Web.ControllerBase { }

    // Both requirements fail: two diagnostics on one type.
    public sealed class BareController { } // ARCH103 T:App.Api.Customers.BareController mustInherit=ControllerBase "BareController" ARCH104 T:App.Api.Customers.BareController mustHaveAttribute=ApiController "BareController"
}
